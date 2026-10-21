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
        long offset = ((long)page - 1) * pageSize;

        if (offset > int.MaxValue)
        {
            return new List<DeliveryResponse>();
        }

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
            .Skip((int)offset)
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

        var previousStatus = delivery.Status;
        var now = DateTime.UtcNow;

        delivery.Status = request.Status;
        delivery.UpdatedAt = now;

        if (request.Status == "Delivered")
        {
            delivery.DeliveredAt = now;
        }

        var history = new DeliveryStatusHistory
        {
            Id = Guid.NewGuid(),
            DeliveryId = delivery.Id,
            PreviousStatus = previousStatus,
            NewStatus = request.Status,
            ChangedAt = now
        };

        dbContext.DeliveryStatusHistories.Add(history);

        await dbContext.SaveChangesAsync();

        return ToResponse(delivery);
    }

    public async Task<DeliveryResponse?> AssignDriverAsync(Guid id, AssignDriverRequest request)
    {
        var delivery = await dbContext.Deliveries.FindAsync(id);

        if (delivery is null)
        {
            return null;
        }

        var driver = await dbContext.Drivers.FindAsync(request.DriverId);

        if (driver is null)
        {
            return null;
        }

        if (!driver.Active)
        {
            throw new InvalidOperationException("Cannot assign an inactive driver.");
        }

        delivery.DriverId = driver.Id;
        delivery.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        return ToResponse(delivery);
    }

    public async Task<List<DeliveryStatusHistoryResponse>> GetHistoryAsync(Guid deliveryId)
    {
        return await dbContext.DeliveryStatusHistories
            .AsNoTracking()
            .Where(h => h.DeliveryId == deliveryId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new DeliveryStatusHistoryResponse
            {
                Id = h.Id,
                PreviousStatus = h.PreviousStatus,
                NewStatus = h.NewStatus,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync();
    }

    private static DeliveryResponse ToResponse(Delivery delivery) => new()
    {
        Id = delivery.Id,
        DriverId = delivery.DriverId,
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
