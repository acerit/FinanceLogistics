using FinanceLogistics.Application.LogisticsTrade;
using Microsoft.Extensions.DependencyInjection;
namespace FinanceLogistics.Infrastructure.LogisticsTrade;
public static class TradeSourceRegistration
{
    // Call AFTER AddPoImports: share the existing database/storage configuration.
    public static IServiceCollection AddTradeSourceImports(this IServiceCollection services)
    {
        services.AddSingleton<TradeFileParser>();
        services.AddScoped<TradeImportFactory>();
        services.AddScoped<ITradeImportFactory>(sp=>sp.GetRequiredService<TradeImportFactory>());
        services.AddScoped<SqlTradeJobProcessor>();
        return services;
    }
}
