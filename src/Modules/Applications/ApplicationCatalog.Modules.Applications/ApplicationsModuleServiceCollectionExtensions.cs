using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ApplicationCatalog.Modules.Applications.Infrastructure;
using ApplicationCatalog.Modules.Applications.Contracts;

namespace ApplicationCatalog.Modules.Applications;

public static class ApplicationsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationsModule(this
    IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IApplicationsModuleApi,
    ApplicationsModuleApi>();

        return services;
    }
}
