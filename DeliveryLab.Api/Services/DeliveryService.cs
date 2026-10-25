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

        ApplyStatusChange(delivery, request.Status, DateTime.UtcNow);

        await dbContext.SaveChangesAsync();

        return ToResponse(delivery);
    }

    private void ApplyStatusChange(Delivery delivery, string status, DateTime changedAt)
    {
        bool isValidTransition =
            (delivery.Status == "Created" &&
                (status == "InTransit" ||
                 status == "Cancelled"))
            ||
            (delivery.Status == "InTransit" &&
                (status == "Delivered" ||
                 status == "Cancelled"));

        if (!isValidTransition)
        {
            throw new InvalidOperationException($"Cannot change status from {delivery.Status} to {status}.");
        }

        var previousStatus = delivery.Status;

        delivery.Status = status;
        delivery.UpdatedAt = changedAt;

        if (status == "Delivered")
        {
            delivery.DeliveredAt = changedAt;
        }

        var history = new DeliveryStatusHistory
        {
            Id = Guid.NewGuid(),
            DeliveryId = delivery.Id,
            PreviousStatus = previousStatus,
            NewStatus = status,
            ChangedAt = changedAt
        };

        dbContext.DeliveryStatusHistories.Add(history);
    }

    public async Task<DeliveryAttemptResponse?> CreateAttemptAsync(Guid id, CreateDeliveryAttemptRequest request)
    {
        var delivery = await dbContext.Deliveries.FindAsync(id);

        if (delivery is null)
        {
            return null;
        }

        if (delivery.Status != "InTransit")
        {
            throw new InvalidOperationException("Only deliveries in transit can receive attempts.");
        }

        var now = DateTime.UtcNow;
        var attempt = new DeliveryAttempt
        {
            Id = Guid.NewGuid(),
            DeliveryId = delivery.Id,
            AttemptedAt = now,
            Successful = request.Successful,
            Notes = request.Notes
        };

        if (request.Successful)
        {
            ApplyStatusChange(delivery, "Delivered", now);
        }

        dbContext.DeliveryAttempts.Add(attempt);

        await dbContext.SaveChangesAsync();

        return ToResponse(attempt);
    }

    public async Task<List<DeliveryAttemptResponse>?> GetAttemptsAsync(Guid id)
    {
        if (!await dbContext.Deliveries.AnyAsync(d => d.Id == id))
        {
            return null;
        }

        var attempts = await dbContext.DeliveryAttempts
            .AsNoTracking()
            .Where(a => a.DeliveryId == id)
            .OrderBy(a => a.AttemptedAt)
            .ThenBy(a => a.Id)
            .ToListAsync();

        return attempts.Select(ToResponse).ToList();
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

    private static DeliveryAttemptResponse ToResponse(DeliveryAttempt attempt) => new()
    {
        Id = attempt.Id,
        DeliveryId = attempt.DeliveryId,
        AttemptedAt = attempt.AttemptedAt,
        Successful = attempt.Successful,
        Notes = attempt.Notes
    };

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
