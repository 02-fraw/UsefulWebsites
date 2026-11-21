using Microsoft.EntityFrameworkCore;
using UsefulWebsites.Models;

namespace UsefulWebsites.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Category> Categories { get; set; } = null!;

    public DbSet<Website> Websites { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(category => category.Id);

            entity.Property(category => category.Name)
                .HasMaxLength(80)
                .IsRequired();

            entity.HasIndex(category => category.Name)
                .IsUnique();

            entity.Property(category => category.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        modelBuilder.Entity<Website>(entity =>
        {
            entity.ToTable("Websites");
            entity.HasKey(website => website.Id);

            entity.Property(website => website.Name)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(website => website.Url)
                .HasMaxLength(2048)
                .IsRequired();

            entity.Property(website => website.LogoUrl)
                .HasMaxLength(2048);

            entity.Property(website => website.Description)
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(website => website.CreatedAtUtc)
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasIndex(website => website.CreatedAtUtc);

            entity.HasIndex(website => new
            {
                website.CategoryId,
                website.CreatedAtUtc
            });

            entity.HasOne(website => website.Category)
                .WithMany(category => category.Websites)
                .HasForeignKey(website => website.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
