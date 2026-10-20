using DeliveryLab.Api.Data;
using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DeliveryLab.Api.Services;

public class DriverService(DeliveryLabDbContext dbContext)
{
    public async Task<DriverResponse> CreateAsync(CreateDriverRequest request)
    {
        var documentAlreadyExists = await dbContext.Drivers
            .AnyAsync(d => d.Document == request.Document);

        if (documentAlreadyExists)
        {
            throw new InvalidOperationException("Driver document already exists.");
        }

        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Document = request.Document,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        return ToResponse(driver);
    }

    public async Task<List<DriverResponse>> GetAllAsync()
    {
        var drivers = await dbContext.Drivers
            .AsNoTracking()
            .OrderBy(d => d.Name)
            .ToListAsync();

        return drivers.Select(ToResponse).ToList();
    }

    public async Task<DriverResponse?> GetByIdAsync(Guid id)
    {
        var driver = await dbContext.Drivers
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id);

        return driver is null ? null : ToResponse(driver);
    }

    private static DriverResponse ToResponse(Driver driver) => new()
    {
        Id = driver.Id,
        Name = driver.Name,
        Document = driver.Document,
        Active = driver.Active,
        CreatedAt = driver.CreatedAt
    };
}