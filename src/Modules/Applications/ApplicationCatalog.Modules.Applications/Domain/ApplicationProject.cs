namespace ApplicationCatalog.Modules.Applications.Domain;

public class ApplicationProject
{
    public required Guid Id { get; init; }
    public required string ProjectName { get; set; }
    public required FrameworkVersionName FrameworkVersion { get; set; }
    public required ProjectTypeName ProjectType { get; set; }
    public ProjectCategoryName ProjectCategory => ProjectType.GetCategory();
}
