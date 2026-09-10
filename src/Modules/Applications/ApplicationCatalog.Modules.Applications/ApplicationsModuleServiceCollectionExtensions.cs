using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ApplicationCatalog.Modules.Applications.Infrastructure;

namespace ApplicationCatalog.Modules.Applications;

public static class ApplicationsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationsModule(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }
}
