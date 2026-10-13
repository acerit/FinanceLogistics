using FinanceLogistics.Application.Imports;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceLogistics.Infrastructure.Imports;

public static class PoImportRegistration
{
    public static IServiceCollection AddPoImports(this IServiceCollection services, string connectionString, string storageRoot)
    {
        services.AddSingleton(new PoDatabase(connectionString));
        services.AddSingleton(new LocalPoFileStorage(storageRoot));
        services.AddSingleton<PoFileParser>();
        services.AddScoped<IFileStorage>(sp => sp.GetRequiredService<LocalPoFileStorage>());
        services.AddScoped<IPoImportService, SqlPoImportService>();
        services.AddScoped<IPoConfirmationStore, SqlPoConfirmationStore>();
        services.AddScoped<ConfirmPoImport>();
        services.AddScoped<IImportJobProcessor, SqlPoJobProcessor>();
        return services;
    }
}
