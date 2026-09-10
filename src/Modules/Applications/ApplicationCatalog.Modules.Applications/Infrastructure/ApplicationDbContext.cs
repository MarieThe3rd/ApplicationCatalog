using Microsoft.EntityFrameworkCore;

namespace ApplicationCatalog.Modules.Applications.Infrastructure;

internal sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    public DbSet<Domain.Application> Applications => Set<Domain.Application>();

    protected override void OnModelCreating(ModelBuilder
    modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("applications");

        modelBuilder.Entity<Domain.Application>()
            .HasMany(a => a.Projects)
            .WithOne()
            .HasForeignKey("ApplicationId")
            .IsRequired();

        modelBuilder.Entity<Domain.Application>()
            .HasMany(a => a.Links)
            .WithOne()
            .HasForeignKey("ApplicationId")
            .IsRequired();
    }
}