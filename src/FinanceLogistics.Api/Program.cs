using FinanceLogistics.Infrastructure.Imports;
using System.Threading.RateLimiting;
using FinanceLogistics.Api.Configuration;
using FinanceLogistics.Api.Contracts;
using FinanceLogistics.Api.Data;
using FinanceLogistics.Api.Repositories;
using FinanceLogistics.Api.Security;
using FinanceLogistics.Api.Services;
using FinanceLogistics.Api.Web;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FinanceLogistics.Infrastructure.Periods;

var builder = WebApplication.CreateBuilder(args);
if (!OperatingSystem.IsWindows())
    throw new PlatformNotSupportedException("This AD adapter targets a Windows/IIS environment.");

var settings = builder.Configuration.GetSection("Identity").Get<IdentitySettings>() ?? new();
settings.Validate();
var connection = builder.Configuration.GetConnectionString("FinanceLogistics");
if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure the FinanceLogistics connection string.");
var keyPath = Path.GetFullPath(settings.KeyRingPath);
var wwwroot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));
if (keyPath.Equals(wwwroot, StringComparison.OrdinalIgnoreCase) || keyPath.StartsWith(wwwroot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Keep the Data Protection key ring outside wwwroot.");
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection().SetApplicationName("FinanceLogistics.Identity.V1")
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath)).ProtectKeysWithDpapi();

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddDbContext<IdentityDbContext>(o => o.UseSqlServer(connection));
builder.Services.AddPoPeriodQueries(connection);
builder.Services.AddPoImports(connection,
    builder.Configuration["PoImport:StorageRoot"]
    ?? throw new InvalidOperationException("Configure PoImport:StorageRoot before starting the API."));
builder.Services.AddScoped<IIdentityStore, IdentityStore>();
builder.Services.AddScoped<IAdAuthenticator, AdAuthenticator>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<SessionCsrfFilter>();

builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "__Host-fl-csrf";
    o.Cookie.HttpOnly = true; o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Cookie.SameSite = SameSiteMode.Strict; o.Cookie.Path = "/";
});
builder.Services.AddControllers().ConfigureApiBehaviorOptions(o => {
    o.InvalidModelStateResponseFactory = c => new BadRequestObjectResult(new ApiError(
        "INVALID_REQUEST", "Dữ liệu yêu cầu không hợp lệ.", c.HttpContext.TraceIdentifier));
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => {
    o.MapInboundClaims = false;
    o.IncludeErrorDetails = false;
    o.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuer = true, ValidIssuer = settings.Issuer,
        ValidateAudience = true, ValidAudience = settings.Audience,
        ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
        ValidateIssuerSigningKey = true, RequireSignedTokens = true,
        IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(settings.SigningKeyBase64)),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256], NameClaimType = "name", RoleClaimType = "role"
    };
    o.Events = new JwtBearerEvents {
        OnMessageReceived = c => {
            // Keep CSRF tokens anonymous and session endpoints cookie-only. No bearer token is read from cookies.
            if (c.Request.Path.StartsWithSegments("/api/auth")) c.NoResult();
            return Task.CompletedTask;
        },
        OnTokenValidated = async c => {
            var subject = c.Principal?.FindFirst("sub")?.Value;
            if (subject is null || !Guid.TryParse(c.Principal?.FindFirst("session_id")?.Value, out var id)) {
                c.Fail("Invalid session."); return;
            }
            var auth = c.HttpContext.RequestServices.GetRequiredService<AuthService>();
            var profile = await auth.ValidateAccessAsync(id, subject, c.HttpContext.RequestAborted);
            if (profile is null) { c.Fail("Session unavailable."); return; }
            // Replace potentially stale JWT roles/sites with CURRENT database authorization.
            c.Principal = TokenService.Principal(profile, id);
            c.HttpContext.Items[CurrentUser.ItemKey] = profile;
        },
        OnChallenge = async c => {
            c.HandleResponse(); c.Response.StatusCode = 401;
            c.Response.Headers.WWWAuthenticate = "Bearer";
            await c.Response.WriteAsJsonAsync(new ApiError("AUTH_REQUIRED", "Vui lòng đăng nhập lại.", c.HttpContext.TraceIdentifier));
        },
        OnForbidden = async c => {
            c.Response.StatusCode = 403;
            await c.Response.WriteAsJsonAsync(new ApiError("FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", c.HttpContext.TraceIdentifier));
        }
    };
});
builder.Services.AddAuthorization(o => {
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("FinanceBusiness", p => p.RequireRole(AccessRules.Finance));
    o.AddPolicy("AccessManagement", p => p.RequireRole(AccessRules.SystemAdmin));
});
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.OnRejected = async (context, ct) => {
        context.HttpContext.Response.Headers.RetryAfter = "300";
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiError("RATE_LIMITED",
            "Có quá nhiều yêu cầu. Vui lòng chờ rồi thử lại.", context.HttpContext.TraceIdentifier), ct);
    };
    o.AddPolicy("login", c => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions {
            PermitLimit = settings.LoginRequestsPerFiveMinutesPerIp, Window = TimeSpan.FromMinutes(5), QueueLimit = 0
        }));
    o.AddPolicy("session", c => RateLimitPartition.GetFixedWindowLimiter(
        c.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseMiddleware<ApiEnvelopeMiddleware>();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles(); // Public shell only. Financial documents must never be placed in wwwroot.
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
// No EnsureCreated/Migrate here: apply the reviewed SQL scripts explicitly in the target development database.
app.Run();

public partial class Program { }
