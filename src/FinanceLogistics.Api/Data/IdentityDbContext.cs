using FinanceLogistics.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceLogistics.Api.Data;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<LoginAlias> LoginAliases => Set<LoginAlias>();
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<IdentityAudit> Audit => Set<IdentityAudit>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<SourceType> Sources => Set<SourceType>();
    public DbSet<SourceScope> SourceScopes => Set<SourceScope>();
    public DbSet<ScopeSite> ScopeSites => Set<ScopeSite>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<SamlLoginRequest> SamlLoginRequests => Set<SamlLoginRequest>();
    public DbSet<SamlReplayProof> SamlReplayProofs => Set<SamlReplayProof>();             

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AppUser>(e => {
            e.ToTable("AppUser", "sec"); e.HasKey(x => x.UserId);
            e.Property(x => x.WindowsSid).HasMaxLength(184);
            e.HasIndex(x => x.WindowsSid).IsUnique();
            e.Property(x => x.Upn).HasMaxLength(320);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.CreatedAtUtc).HasPrecision(3);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasMany(x => x.Sites).WithOne().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne(x => x.DirectoryAccount).WithOne().HasForeignKey<DirectoryAccount>(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<AppRole>(e => {
            e.ToTable("AppRole", "sec"); e.HasKey(x => x.RoleCode);
            e.Property(x => x.RoleCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.RoleName).HasMaxLength(120);
        });
        m.Entity<UserRole>(e => {
            e.ToTable("UserRole", "sec"); e.HasKey(x => new { x.UserId, x.RoleCode });
            e.Property(x => x.RoleCode).HasMaxLength(30).IsUnicode(false);
            e.Property(x => x.GrantedAtUtc).HasPrecision(3);
            e.HasOne<AppRole>().WithMany().HasForeignKey(x => x.RoleCode).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<Site>(e => {
            e.ToTable("Site", "cfg"); e.HasKey(x => x.SiteCode);
            e.Property(x => x.SiteCode).HasMaxLength(20); e.Property(x => x.SiteName).HasMaxLength(200);
        });
        m.Entity<UserSite>(e => {
            e.ToTable("UserSite", "sec"); e.HasKey(x => new { x.UserId, x.SiteCode });
            e.Property(x => x.SiteCode).HasMaxLength(20);
            e.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteCode).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<DirectoryAccount>(e => {
            e.ToTable("DirectoryAccount", "sec"); e.HasKey(x => x.UserId);
            e.Property(x => x.DirectoryKey).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.SamAccountName).HasMaxLength(256);
            e.HasIndex(x => new { x.DirectoryKey, x.SamAccountName }).IsUnique();
        });
        m.Entity<LoginAlias>(e => {
            e.ToTable("LoginAlias", "sec"); e.HasKey(x => x.NormalizedAlias);
            e.Property(x => x.NormalizedAlias).HasMaxLength(320);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<AuthSession>(e => {
            e.ToTable("AuthSession", "sec"); e.HasKey(x => x.SessionId);
            e.Property(x => x.SecretHash).HasColumnType("binary(32)");
            e.Property(x => x.CreatedAtUtc).HasPrecision(3);
            e.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            e.Property(x => x.RevokedAtUtc).HasPrecision(3);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.Property(x => x.AuthenticationMethod).HasMaxLength(8).IsUnicode(false).HasDefaultValue("LDAP");
            e.HasOne(x => x.ExternalIdentity).WithMany().HasForeignKey(x => x.ExternalIdentityId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<IdentityAudit>(e => {
            e.ToTable("IdentityAudit", "sec"); e.HasKey(x => x.EventId);
            e.Property(x => x.EventCode).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.CorrelationId).HasMaxLength(64);
            e.Property(x => x.OccurredAtUtc).HasPrecision(3);
        });
        m.Entity<DataScope>(e => {
            e.ToTable("DataScope", "cfg"); e.HasKey(x => x.ScopeCode);
            e.Property(x => x.ScopeCode).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.ScopeName).HasMaxLength(200);
        });
        m.Entity<ScopeSite>(e => {
            e.ToTable("ScopeSite", "cfg"); e.HasKey(x => new { x.ScopeCode, x.SiteCode });
            e.Property(x => x.ScopeCode).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.SiteCode).HasMaxLength(20);
        });
        m.Entity<SourceType>(e => {
            e.ToTable("SourceType", "cfg"); e.HasKey(x => x.SourceCode);
            e.Property(x => x.SourceCode).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.SourceName).HasMaxLength(200);
            e.Property(x => x.OwnerRoleCode).HasMaxLength(30).IsUnicode(false);
        });
        m.Entity<SourceScope>(e => {
            e.ToTable("SourceScope", "cfg"); e.HasKey(x => new { x.SourceCode, x.ScopeCode });
            e.Property(x => x.SourceCode).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.ScopeCode).HasMaxLength(40).IsUnicode(false);
        });
        m.Entity<ExternalIdentity>(e => {
            e.ToTable("ExternalIdentity", "sec"); e.HasKey(x => x.ExternalIdentityId);
            e.Property(x => x.ProviderKey).HasMaxLength(40).IsUnicode(false);
            e.Property(x => x.CreatedAtUtc).HasPrecision(3);
            e.HasIndex(x => new { x.ProviderKey, x.TenantId, x.ObjectId }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.NoAction);
        });
        m.Entity<SamlLoginRequest>(e =>
        {
            e.ToTable("SamlLoginRequest", "sec");
            e.HasKey(x => x.RequestId);
            e.Property(x => x.RequestId).HasMaxLength(128).IsUnicode(false).UseCollation("Latin1_General_100_BIN2");
            e.Property(x => x.BrowserSecretHash).HasColumnType("binary(32)");
            e.Property(x => x.CreatedAtUtc).HasPrecision(3);
            e.Property(x => x.ExpiresAtUtc).HasPrecision(3);
            e.Property(x => x.ConsumedAtUtc).HasPrecision(3);
        });
        m.Entity<SamlReplayProof>(e =>
        {
            e.ToTable("SamlReplayProof", "sec");
            e.HasKey(x => x.ProofHash);
            e.Property(x => x.ProofHash).HasColumnType("binary(32)");
            e.Property(x => x.ExpiresAtUtc).HasPrecision(3);
        });
    }
}
