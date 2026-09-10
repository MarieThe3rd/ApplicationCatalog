using ApplicationCatalog.Modules.Applications.Domain;

namespace ApplicationCatalog.Modules.Applications.Contracts;

public sealed class ApplicationSummary
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required ApplicationStatus Status { get; init; }
    public string? BusinessOwner { get; init; } 
    public string? ItOwner { get; init; }
}
