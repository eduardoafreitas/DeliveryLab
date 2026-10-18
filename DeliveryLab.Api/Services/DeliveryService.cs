using DeliveryLab.Api.Data;
using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryLab.Api.Services;

public class DeliveryService(DeliveryLabDbContext dbContext)
{
    public async Task<DeliveryResponse> CreateAsync(CreateDeliveryRequest request)
    {
        var id = Guid.NewGuid();
        var delivery = new Delivery
        {
            Id = id,
            TrackingCode = $"DL-{id:N}",
            RecipientName = request.RecipientName,
            OriginAddress = request.OriginAddress,
            DestinationAddress = request.DestinationAddress,
            Status = "Created",
            CreatedAt = DateTime.UtcNow,
            EstimatedDeliveryDate  = request.EstimatedDeliveryDate
        };

        dbContext.Deliveries.Add(delivery);
        await dbContext.SaveChangesAsync();

        return ToResponse(delivery);
    }

    public async Task<List<DeliveryResponse>> GetAllAsync(
        string? status,
        int page,
        int pageSize)
    {
        var query = dbContext.Deliveries
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(d => d.Status == status);
        }

        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return deliveries.Select(ToResponse).ToList();
    }

    public async Task<DeliveryResponse?> GetByIdAsync(Guid id)
    {
        var delivery = await dbContext.Deliveries.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id);

        return delivery is null ? null : ToResponse(delivery);
    }

    public async Task<DeliveryResponse?> GetByTrackingCodeAsync(string trackingCode)
    {
        var delivery = await dbContext.Deliveries.AsNoTracking()
            .SingleOrDefaultAsync(d => d.TrackingCode == trackingCode);

        return delivery is null ? null : ToResponse(delivery);
    }

    public async Task<DeliveryResponse?> UpdateStatusAsync(Guid id,UpdateDeliveryStatusRequest request)
    {
        var delivery = await dbContext.Deliveries.FindAsync(id);

        if (delivery is null)
        {
            return null;
        }

        bool isValidTransition =
            (delivery.Status == "Created" &&
                (request.Status == "InTransit" ||
                 request.Status == "Cancelled"))
            ||
            (delivery.Status == "InTransit" &&
                (request.Status == "Delivered" ||
                 request.Status == "Cancelled"));

        if (!isValidTransition)
        {
            throw new InvalidOperationException($"Cannot change status from {delivery.Status} to {request.Status}.");
        }

        var now = DateTime.UtcNow;

        delivery.Status = request.Status;
        delivery.UpdatedAt = now;

        if (request.Status == "Delivered")
        {
            delivery.DeliveredAt = now;
        }

        await dbContext.SaveChangesAsync();

        return ToResponse(delivery);
    }

    private static DeliveryResponse ToResponse(Delivery delivery) => new()
    {
        Id = delivery.Id,
        TrackingCode = delivery.TrackingCode,
        RecipientName = delivery.RecipientName,
        OriginAddress = delivery.OriginAddress,
        DestinationAddress = delivery.DestinationAddress,
        Status = delivery.Status,
        CreatedAt = delivery.CreatedAt,
        UpdatedAt = delivery.UpdatedAt,
        EstimatedDeliveryDate = delivery.EstimatedDeliveryDate,
        DeliveredAt = delivery.DeliveredAt,
    };
}
