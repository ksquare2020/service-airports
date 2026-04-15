using Microsoft.EntityFrameworkCore;
using ServiceAirports.Api.Models;

namespace ServiceAirports.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Airport> Airports => Set<Airport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Airport>(entity =>
        {
            entity.ToTable("Airports");
            entity.HasKey(airport => airport.Id);
            entity.Property(airport => airport.Name).HasMaxLength(200).IsRequired();
            entity.Property(airport => airport.Code).HasMaxLength(10).IsRequired();
            entity.Property(airport => airport.City).HasMaxLength(100).IsRequired();
            entity.Property(airport => airport.Country).HasMaxLength(100).IsRequired();
        });
    }
}
