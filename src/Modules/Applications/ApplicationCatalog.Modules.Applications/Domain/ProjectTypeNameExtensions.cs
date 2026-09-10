namespace ApplicationCatalog.Modules.Applications.Domain;

public static class ProjectTypeNameExtensions
{
    extension(ProjectTypeName type)
    {
        public ProjectCategoryName GetCategory()
=> type switch
{
    ProjectTypeName.WebFormsApplication
          or ProjectTypeName.WebMvcApplication
          or ProjectTypeName.WebRazorPagesApplication
          or ProjectTypeName.BlazorApplication => ProjectCategoryName.Web,

    ProjectTypeName.WebApiMvc
          or ProjectTypeName.WebApiMinimal => ProjectCategoryName.WebService,

    ProjectTypeName.WorkerService
          or ProjectTypeName.WindowsService => ProjectCategoryName.Service,

    ProjectTypeName.ConsoleApplication => ProjectCategoryName.ScheduledJob,

    ProjectTypeName.WindowsFormsApplication
          or ProjectTypeName.WpfApplication
          or ProjectTypeName.MauiApplication => ProjectCategoryName.Desktop,

    ProjectTypeName.ClassLibrary => ProjectCategoryName.Library,

    ProjectTypeName.UnitTestProject
          or ProjectTypeName.IntegrationTestProject
          or ProjectTypeName.UiTestProject => ProjectCategoryName.Test,

    _ => throw new ArgumentOutOfRangeException(nameof(type))
};

        public bool IsTestProject() =>
    type.GetCategory() == ProjectCategoryName.Test;
    }
}