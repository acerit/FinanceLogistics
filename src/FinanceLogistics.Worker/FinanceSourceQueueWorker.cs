using FinanceLogistics.Infrastructure.FinanceSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace FinanceLogistics.Worker;
public sealed class FinanceSourceQueueWorker(IServiceScopeFactory scopes,ILogger<FinanceSourceQueueWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Finance four-source worker started; job type FINANCE_VALIDATE.");
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                if(!await scope.ServiceProvider.GetRequiredService<SqlFinanceJobProcessor>().TryProcessNextAsync(ct))await Task.Delay(TimeSpan.FromSeconds(2),ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e)
            {
                logger.LogWarning("Finance queue unavailable: {ExceptionType}; retry in 10 seconds.",e.GetType().Name);
                try{await Task.Delay(TimeSpan.FromSeconds(10),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            }
        }
    }
}
