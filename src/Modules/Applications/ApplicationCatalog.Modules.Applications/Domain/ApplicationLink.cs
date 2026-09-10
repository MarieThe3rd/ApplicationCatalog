namespace ApplicationCatalog.Modules.Applications.Domain;

public class ApplicationLink
{
    public required Guid Id { get; init; }
    public required string Label { get; set; }
    public required string Url { get; set; }
}