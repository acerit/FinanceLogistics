using FinanceLogistics.Application.Periods;
using FinanceLogistics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceLogistics.Infrastructure.Periods;

public static class PoPeriodRegistration
{
    public static IServiceCollection AddPoPeriodQueries(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ImportDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IPoPeriodReader, SqlPoPeriodReader>();
        return services;
    }
}
