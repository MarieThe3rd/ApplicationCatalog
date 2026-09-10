namespace ApplicationCatalog.Modules.Applications.Domain;

public class Application
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public List<string> AliasNames { get; set; } = new List<string>();
    public string? Description { get; set; }
    public string? BusinessOwner { get; set; }
    public string? ItOwner { get; set; }
    public required ApplicationStatus Status { get; set; }
    public required List<ApplicationProject> Projects { get; set; }
    public List<ApplicationLink> Links { get; set; } = [];
}
