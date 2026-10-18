using System.Net;
using System.Net.Http.Json;
using DeliveryLab.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
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
        Assert.NotNull(response.Headers.Location);

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>(response.Headers.Location);
        Assert.NotNull(persisted);
        Assert.Equal(delivery.Id, persisted.Id);
        Assert.Equal(delivery.TrackingCode, persisted.TrackingCode);
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

    private static CreateDeliveryRequest NewDeliveryRequest() => new()
    {
        RecipientName = "Cliente de teste",
        OriginAddress = "São Paulo - SP",
        DestinationAddress = "Campinas - SP"
    };
}
