using FinanceLogistics.Application.FinanceSources;
using Microsoft.Extensions.DependencyInjection;
namespace FinanceLogistics.Infrastructure.FinanceSources;
public static class FinanceSourceRegistration
{
    // Call AFTER AddPoImports: share the existing database/storage configuration.
    public static IServiceCollection AddFinanceSourceImports(this IServiceCollection services)
    {
        services.AddSingleton<FinanceFileParser>();
        services.AddScoped<FinanceImportFactory>();
        services.AddScoped<IFinanceImportFactory>(sp=>sp.GetRequiredService<FinanceImportFactory>());
        services.AddScoped<SqlFinanceJobProcessor>();
        return services;
    }
}
