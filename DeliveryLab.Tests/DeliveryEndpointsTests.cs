using System.Net;
using System.Net.Http.Json;
using DeliveryLab.Api.Data;
using DeliveryLab.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryLab.Tests;

public class DeliveryEndpointsTests
{
    [Fact]
    public async Task Create_ReturnsCreatedDeliveryAndPersistsIt()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var request = NewDeliveryRequest();
        var before = DateTime.UtcNow;

        var response = await client.PostAsJsonAsync("/api/deliveries", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var delivery = await response.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);
        Assert.NotEqual(Guid.Empty, delivery.Id);
        Assert.False(string.IsNullOrWhiteSpace(delivery.TrackingCode));
        Assert.Equal(request.RecipientName, delivery.RecipientName);
        Assert.Equal(request.OriginAddress, delivery.OriginAddress);
        Assert.Equal(request.DestinationAddress, delivery.DestinationAddress);
        Assert.Equal("Created", delivery.Status);
        Assert.InRange(delivery.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, delivery.CreatedAt.Kind);
        Assert.Null(delivery.UpdatedAt);
        Assert.Null(delivery.DriverId);
        Assert.NotNull(response.Headers.Location);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>(response.Headers.Location);
        Assert.NotNull(persisted);
        Assert.Equal(delivery.Id, persisted.Id);
        Assert.Equal(delivery.TrackingCode, persisted.TrackingCode);
        Assert.Null(persisted.DriverId);
    }

    [Fact]
    public async Task GetById_WhenDeliveryDoesNotExist_ReturnsNotFound()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_PersistsStatusAndUpdatedAt()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest());
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);
        var before = DateTime.UtcNow;

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest { Status = "InTransit" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(updated);
        Assert.Equal("InTransit", updated.Status);
        Assert.Equal(delivery.CreatedAt, updated.CreatedAt);
        Assert.NotNull(updated.UpdatedAt);
        Assert.InRange(updated.UpdatedAt.Value, before, DateTime.UtcNow);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Equal(updated.Status, persisted.Status);
        Assert.Equal(updated.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task UpdateStatus_WhenInvalid_ReturnsBadRequestAndKeepsDeliveryUnchanged()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest());
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest { Status = "Invalid" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(error);
        Assert.Contains("Status", error.Errors.Keys);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Equal("Created", persisted.Status);
        Assert.Null(persisted.UpdatedAt);
    }

    [Fact]
    public async Task UpdateStatus_WhenValidTransition_CreatesHistory()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        var created = await client.PostAsJsonAsync(
            "/api/deliveries",
            NewDeliveryRequest());

        created.EnsureSuccessStatusCode();

        var delivery = await created.Content
            .ReadFromJsonAsync<DeliveryResponse>();

        Assert.NotNull(delivery);

        var updateResponse = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "InTransit"
            });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var history = await client.GetFromJsonAsync<
            List<DeliveryStatusHistoryResponse>>(
            $"/api/deliveries/{delivery.Id}/history");

        Assert.NotNull(history);
        Assert.Single(history);

        Assert.Equal("Created", history[0].PreviousStatus);
        Assert.Equal("InTransit", history[0].NewStatus);
    }

    [Fact]
    public async Task UpdateStatus_WhenInvalidTransition_DoesNotCreateHistory()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        var created = await client.PostAsJsonAsync(
            "/api/deliveries",
            NewDeliveryRequest());

        created.EnsureSuccessStatusCode();

        var delivery = await created.Content
            .ReadFromJsonAsync<DeliveryResponse>();

        Assert.NotNull(delivery);

        var firstUpdate = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "InTransit"
            });

        firstUpdate.EnsureSuccessStatusCode();

        var secondUpdate = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "Delivered"
            });

        secondUpdate.EnsureSuccessStatusCode();

        var invalidUpdate = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "InTransit"
            });

        Assert.Equal(HttpStatusCode.BadRequest, invalidUpdate.StatusCode);

        var history = await client.GetFromJsonAsync<
            List<DeliveryStatusHistoryResponse>>(
            $"/api/deliveries/{delivery.Id}/history");

        Assert.NotNull(history);
        Assert.Equal(2, history.Count);

        Assert.Equal("Created", history[0].PreviousStatus);
        Assert.Equal("InTransit", history[0].NewStatus);

        Assert.Equal("InTransit", history[1].PreviousStatus);
        Assert.Equal("Delivered", history[1].NewStatus);
    }

    [Fact]
    public async Task Cancel_WhenCreated_ReturnsOkAndPersistsCancelledStatus()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        var created = await client.PostAsJsonAsync(
            "/api/deliveries",
            NewDeliveryRequest());

        created.EnsureSuccessStatusCode();

        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);

        var response = await client.PatchAsync(
            $"/api/deliveries/{delivery.Id}/cancel",
            null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cancelled = await response.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(cancelled);
        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.UpdatedAt);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>(
            $"/api/deliveries/{delivery.Id}");

        Assert.NotNull(persisted);
        Assert.Equal("Cancelled", persisted.Status);
        Assert.Equal(cancelled.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task Cancel_WhenDelivered_ReturnsBadRequestAndKeepsDeliveredStatus()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        var created = await client.PostAsJsonAsync(
            "/api/deliveries",
            NewDeliveryRequest());

        created.EnsureSuccessStatusCode();

        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);

        var inTransitResponse = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "InTransit"
            });

        inTransitResponse.EnsureSuccessStatusCode();

        var deliveredResponse = await client.PatchAsJsonAsync(
            $"/api/deliveries/{delivery.Id}/status",
            new UpdateDeliveryStatusRequest
            {
                Status = "Delivered"
            });

        deliveredResponse.EnsureSuccessStatusCode();

        var response = await client.PatchAsync(
            $"/api/deliveries/{delivery.Id}/cancel",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>(
            $"/api/deliveries/{delivery.Id}");

        Assert.NotNull(persisted);
        Assert.Equal("Delivered", persisted.Status);
        Assert.NotNull(persisted.DeliveredAt);
    }

    [Fact]
    public async Task AssignDriver_WhenActive_ReturnsOkAndPersistsDriverIdAndUpdatedAt()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest());
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);
        var createdDriver = await client.PostAsJsonAsync("/api/drivers",
            new CreateDriverRequest { Name = "Motorista de teste", Document = "12345678900" });
        createdDriver.EnsureSuccessStatusCode();
        var driver = await createdDriver.Content.ReadFromJsonAsync<DriverResponse>();
        Assert.NotNull(driver);
        var before = DateTime.UtcNow;

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/driver",
            new AssignDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(updated);
        Assert.Equal(delivery.Id, updated.Id);
        Assert.Equal(driver.Id, updated.DriverId);
        Assert.Equal(delivery.CreatedAt, updated.CreatedAt);
        Assert.Equal(delivery.Status, updated.Status);
        Assert.NotNull(updated.UpdatedAt);
        Assert.InRange(updated.UpdatedAt.Value, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, updated.UpdatedAt.Value.Kind);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Equal(driver.Id, persisted.DriverId);
        Assert.Equal(updated.UpdatedAt, persisted.UpdatedAt);
    }

    [Fact]
    public async Task AssignDriver_WhenDeliveryDoesNotExist_ReturnsNotFound()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var createdDriver = await client.PostAsJsonAsync("/api/drivers",
            new CreateDriverRequest { Name = "Motorista de teste", Document = "12345678900" });
        createdDriver.EnsureSuccessStatusCode();
        var driver = await createdDriver.Content.ReadFromJsonAsync<DriverResponse>();
        Assert.NotNull(driver);

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{Guid.NewGuid()}/driver",
            new AssignDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignDriver_WhenDriverDoesNotExist_ReturnsNotFoundAndKeepsDeliveryUnchanged()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest());
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/driver",
            new AssignDriverRequest { DriverId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Null(persisted.DriverId);
        Assert.Equal(delivery.UpdatedAt, persisted.UpdatedAt);
        Assert.Equal(delivery.Status, persisted.Status);
    }

    [Fact]
    public async Task AssignDriver_WhenInactive_ReturnsBadRequestAndKeepsDeliveryUnchanged()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var created = await client.PostAsJsonAsync("/api/deliveries", NewDeliveryRequest());
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);
        var createdDriver = await client.PostAsJsonAsync("/api/drivers",
            new CreateDriverRequest { Name = "Motorista de teste", Document = "12345678900" });
        createdDriver.EnsureSuccessStatusCode();
        var driver = await createdDriver.Content.ReadFromJsonAsync<DriverResponse>();
        Assert.NotNull(driver);

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
            var inactiveDriver = await dbContext.Drivers.FindAsync(driver.Id);
            Assert.NotNull(inactiveDriver);
            inactiveDriver.Active = false;
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/driver",
            new AssignDriverRequest { DriverId = driver.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Cannot assign an inactive driver.", await response.Content.ReadAsStringAsync());
        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Null(persisted.DriverId);
        Assert.Equal(delivery.UpdatedAt, persisted.UpdatedAt);
        Assert.Equal(delivery.Status, persisted.Status);
    }

    private static CreateDeliveryRequest NewDeliveryRequest() => new()
    {
        RecipientName = "Cliente de teste",
        OriginAddress = "São Paulo - SP",
        DestinationAddress = "Campinas - SP"
    };
}
