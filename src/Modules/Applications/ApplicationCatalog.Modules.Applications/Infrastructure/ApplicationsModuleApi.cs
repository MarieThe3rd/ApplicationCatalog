using Microsoft.EntityFrameworkCore;
using ApplicationCatalog.Modules.Applications.Contracts;


namespace ApplicationCatalog.Modules.Applications.Infrastructure;

internal sealed class ApplicationsModuleApi : IApplicationsModuleApi
{
    private readonly ApplicationDbContext _dbContext;
    public ApplicationsModuleApi(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<IReadOnlyList<ApplicationSummary>> GetApplicationsAsync()
    {
        return await _dbContext.Applications.Select(a => new ApplicationSummary
        {
            Id = a.Id,
            Name = a.Name,
            Status = a.Status,
            BusinessOwner = a.BusinessOwner,
            ItOwner = a.ItOwner
        }).ToListAsync();
    }
}