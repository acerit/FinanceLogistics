using FinanceLogistics.Application.Imports;
using FinanceLogistics.Infrastructure.Imports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
if (builder.Environment.IsDevelopment()) builder.Configuration.AddUserSecrets<Program>(optional: true);
var connection = builder.Configuration.GetConnectionString("FinanceLogistics")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:FinanceLogistics. Start the DEV worker with DOTNET_ENVIRONMENT=Development.");
var storage = builder.Configuration["PoImport:StorageRoot"]
    ?? throw new InvalidOperationException("Missing PoImport:StorageRoot. API and Worker must use the SAME private directory.");
builder.Services.AddPoImports(connection, storage);
builder.Services.AddHostedService<PoQueueWorker>();
await builder.Build().RunAsync();

internal sealed class PoQueueWorker(IServiceScopeFactory scopes, ILogger<PoQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PO worker started. Durable SQL queue; stop with Ctrl+C.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var worked = await scope.ServiceProvider.GetRequiredService<IImportJobProcessor>().TryProcessNextAsync(stoppingToken);
                if (!worked) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogWarning("PO queue unavailable ({ExceptionType}); retry in 10 seconds. Check SQL/storage configuration.", ex.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }
}
