using FinanceLogistics.Infrastructure.LogisticsTrade;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace FinanceLogistics.Worker;
public sealed class TradeSourceQueueWorker(IServiceScopeFactory scopes,ILogger<TradeSourceQueueWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("NK two-site worker started; job type TRADE_VALIDATE.");
        while(!ct.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                if(!await scope.ServiceProvider.GetRequiredService<SqlTradeJobProcessor>().TryProcessNextAsync(ct))await Task.Delay(TimeSpan.FromSeconds(2),ct);
            }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            catch(Exception e)
            {
                logger.LogWarning("NK queue unavailable: {ExceptionType}; retry in 10 seconds.",e.GetType().Name);
                try{await Task.Delay(TimeSpan.FromSeconds(10),ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){break;}
            }
        }
    }
}
