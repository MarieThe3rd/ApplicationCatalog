namespace ApplicationCatalog.Modules.Applications.Contracts;

public interface IApplicationsModuleApi
{
    Task<IReadOnlyList<ApplicationSummary>>
GetApplicationsAsync();
}
