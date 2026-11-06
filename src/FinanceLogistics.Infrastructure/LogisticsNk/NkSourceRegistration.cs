using FinanceLogistics.Application.LogisticsNk;
using Microsoft.Extensions.DependencyInjection;
namespace FinanceLogistics.Infrastructure.LogisticsNk;
public static class NkSourceRegistration
{
    // Call AFTER AddPoImports: share the existing database/storage configuration.
    public static IServiceCollection AddNkSourceImports(this IServiceCollection services)
    {
        services.AddSingleton<NkFileParser>();
        services.AddScoped<NkImportFactory>();
        services.AddScoped<INkImportFactory>(sp=>sp.GetRequiredService<NkImportFactory>());
        services.AddScoped<SqlNkJobProcessor>();
        return services;
    }
}
