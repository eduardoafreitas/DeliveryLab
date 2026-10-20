using DeliveryLab.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryLab.Api.Data;

public class DeliveryLabDbContext(DbContextOptions<DeliveryLabDbContext> options)
    : DbContext(options)
{
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<DeliveryStatusHistory> DeliveryStatusHistories => Set<DeliveryStatusHistory>();

    public DbSet<Driver> Drivers => Set<Driver>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var delivery = modelBuilder.Entity<Delivery>();

        delivery.HasKey(d => d.Id);
        delivery.Property(d => d.TrackingCode).IsRequired();
        delivery.HasIndex(d => d.TrackingCode).IsUnique();
        delivery.Property(d => d.RecipientName).IsRequired();
        delivery.Property(d => d.OriginAddress).IsRequired();
        delivery.Property(d => d.DestinationAddress).IsRequired();
        delivery.Property(d => d.Status).IsRequired();

        modelBuilder.Entity<DeliveryStatusHistory>()
        .HasOne<Delivery>()
        .WithMany()
        .HasForeignKey(h => h.DeliveryId);

        var driver = modelBuilder.Entity<Driver>();

        driver.HasKey(d => d.Id);
        driver.Property(d => d.Name).IsRequired();
        driver.Property(d => d.Document).IsRequired();
        driver.HasIndex(d => d.Document).IsUnique();

        // Valores fixos para que o seed não gere alterações a cada migration.
        delivery.HasData(
            new Delivery
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                TrackingCode = "DL-11111111111111111111111111111111",
                RecipientName = "Cliente Teste 1",
                OriginAddress = "São Paulo - SP",
                DestinationAddress = "Campinas - SP",
                Status = "Created",
                CreatedAt = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc)
            },
            new Delivery
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                TrackingCode = "DL-22222222222222222222222222222222",
                RecipientName = "Cliente Teste 2",
                OriginAddress = "São José do Rio Preto - SP",
                DestinationAddress = "Ribeirão Preto - SP",
                Status = "Created",
                CreatedAt = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc)
            });
    }
}
