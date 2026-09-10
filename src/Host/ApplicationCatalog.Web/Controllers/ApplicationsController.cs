using Microsoft.AspNetCore.Mvc;
using ApplicationCatalog.Modules.Applications.Contracts;

namespace ApplicationCatalog.Web.Controllers;

public class ApplicationsController : Controller
{
    private readonly IApplicationsModuleApi _applicationsModuleApi;

    public ApplicationsController(IApplicationsModuleApi applicationsModuleApi)
    {
        _applicationsModuleApi = applicationsModuleApi;
    }

    public async Task<IActionResult> Index()
    {
        var applications = await
            _applicationsModuleApi.GetApplicationsAsync();
        return View(applications);
    }
}
