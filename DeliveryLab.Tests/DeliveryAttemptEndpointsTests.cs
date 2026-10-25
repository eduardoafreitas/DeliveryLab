using System.Net;
using System.Net.Http.Json;
using DeliveryLab.Api.Data;
using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Models;
using DeliveryLab.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryLab.Tests;

[Trait("Task", "DEL-013")]
public class DeliveryAttemptEndpointsTests
{
    [Theory]
    [InlineData("Destinatário ausente")]
    [InlineData(null)]
    public async Task CreateAttempt_WhenUnsuccessful_PersistsAttemptAndKeepsDeliveryAndHistoryUnchanged(string? notes)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client, "InTransit");
        var historyBefore = await GetHistoryAsync(client, delivery.Id);
        var before = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/attempts",
            new CreateDeliveryAttemptRequest { Successful = false, Notes = notes });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempt = await response.Content.ReadFromJsonAsync<DeliveryAttemptResponse>();
        Assert.NotNull(attempt);
        Assert.NotEqual(Guid.Empty, attempt.Id);
        Assert.Equal(delivery.Id, attempt.DeliveryId);
        Assert.False(attempt.Successful);
        Assert.Equal(notes, attempt.Notes);
        Assert.InRange(attempt.AttemptedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, attempt.AttemptedAt.Kind);

        var persistedAttempts = await client.GetFromJsonAsync<List<DeliveryAttemptResponse>>(
            $"/api/deliveries/{delivery.Id}/attempts");
        Assert.NotNull(persistedAttempts);
        var persistedAttempt = Assert.Single(persistedAttempts);
        Assert.Equal(attempt.Id, persistedAttempt.Id);
        Assert.Equal(attempt.DeliveryId, persistedAttempt.DeliveryId);
        Assert.Equal(attempt.AttemptedAt, persistedAttempt.AttemptedAt);
        Assert.False(persistedAttempt.Successful);
        Assert.Equal(notes, persistedAttempt.Notes);

        var persistedDelivery = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persistedDelivery);
        Assert.Equal("InTransit", persistedDelivery.Status);
        Assert.Null(persistedDelivery.DeliveredAt);
        Assert.Equal(delivery.UpdatedAt, persistedDelivery.UpdatedAt);
        var historyAfter = await GetHistoryAsync(client, delivery.Id);
        Assert.Equal(historyBefore.Select(h => h.Id), historyAfter.Select(h => h.Id));
    }

    [Fact]
    public async Task CreateAttempt_WhenSuccessful_PersistsAttemptDeliveryAndHistoryWithSameTimestamp()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client, "InTransit");
        var before = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/attempts",
            new CreateDeliveryAttemptRequest { Successful = true, Notes = "Recebido pelo destinatário" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempt = await response.Content.ReadFromJsonAsync<DeliveryAttemptResponse>();
        Assert.NotNull(attempt);
        Assert.NotEqual(Guid.Empty, attempt.Id);
        Assert.Equal(delivery.Id, attempt.DeliveryId);
        Assert.True(attempt.Successful);
        Assert.Equal("Recebido pelo destinatário", attempt.Notes);
        Assert.InRange(attempt.AttemptedAt, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, attempt.AttemptedAt.Kind);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
        var persistedAttempt = await dbContext.DeliveryAttempts.SingleAsync(a => a.DeliveryId == delivery.Id);
        Assert.Equal(attempt.Id, persistedAttempt.Id);
        Assert.Equal(attempt.AttemptedAt, persistedAttempt.AttemptedAt);
        Assert.True(persistedAttempt.Successful);
        Assert.Equal(attempt.Notes, persistedAttempt.Notes);

        var persistedDelivery = await dbContext.Deliveries.SingleAsync(d => d.Id == delivery.Id);
        Assert.Equal("Delivered", persistedDelivery.Status);
        Assert.NotNull(persistedDelivery.DeliveredAt);
        Assert.NotNull(persistedDelivery.UpdatedAt);
        Assert.Equal(attempt.AttemptedAt, persistedDelivery.DeliveredAt);
        Assert.Equal(attempt.AttemptedAt, persistedDelivery.UpdatedAt);
        Assert.Equal(delivery.CreatedAt, persistedDelivery.CreatedAt);

        var history = await GetHistoryAsync(client, delivery.Id);
        Assert.Equal(2, history.Count);
        var deliveredHistory = Assert.Single(history, h => h.NewStatus == "Delivered");
        Assert.Equal("InTransit", deliveredHistory.PreviousStatus);
        Assert.Equal(attempt.AttemptedAt, deliveredHistory.ChangedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateAttempt_WhenDeliveryDoesNotExist_ReturnsNotFound(bool successful)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.PostAsJsonAsync($"/api/deliveries/{Guid.NewGuid()}/attempts",
            new CreateDeliveryAttemptRequest { Successful = successful });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
        Assert.Empty(await dbContext.DeliveryAttempts.ToListAsync());
        Assert.Empty(await dbContext.DeliveryStatusHistories.ToListAsync());
    }

    [Theory]
    [InlineData("Created", false)]
    [InlineData("Created", true)]
    [InlineData("Delivered", false)]
    [InlineData("Delivered", true)]
    [InlineData("Cancelled", false)]
    [InlineData("Cancelled", true)]
    public async Task CreateAttempt_WhenStatusIsNotInTransit_ReturnsBadRequestAndPersistsNoChanges(
        string status, bool successful)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client, status);
        var historyBefore = await GetHistoryAsync(client, delivery.Id);

        using var response = await client.PostAsJsonAsync($"/api/deliveries/{delivery.Id}/attempts",
            new CreateDeliveryAttemptRequest { Successful = successful });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var attempts = await client.GetFromJsonAsync<List<DeliveryAttemptResponse>>(
            $"/api/deliveries/{delivery.Id}/attempts");
        Assert.NotNull(attempts);
        Assert.Empty(attempts);
        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Equal(delivery.Status, persisted.Status);
        Assert.Equal(delivery.DeliveredAt, persisted.DeliveredAt);
        Assert.Equal(delivery.UpdatedAt, persisted.UpdatedAt);
        var historyAfter = await GetHistoryAsync(client, delivery.Id);
        Assert.Equal(historyBefore.Select(h => h.Id), historyAfter.Select(h => h.Id));
    }

    [Fact]
    public async Task GetAttempts_WhenDeliveryHasNoAttempts_ReturnsOkWithEmptyList()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client);

        using var response = await client.GetAsync($"/api/deliveries/{delivery.Id}/attempts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempts = await response.Content.ReadFromJsonAsync<List<DeliveryAttemptResponse>>();
        Assert.NotNull(attempts);
        Assert.Empty(attempts);
    }

    [Fact]
    public async Task GetAttempts_ReturnsOnlyRequestedDeliveryAttemptsOrderedByTimestampThenId()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client, "InTransit");
        var otherDelivery = await CreateDeliveryAsync(client, "InTransit");
        var now = DateTime.UtcNow;
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var lastId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
            dbContext.DeliveryAttempts.AddRange(
                new DeliveryAttempt { Id = lastId, DeliveryId = delivery.Id, AttemptedAt = now, Successful = true, Notes = "Entregue" },
                new DeliveryAttempt { Id = secondId, DeliveryId = delivery.Id, AttemptedAt = now.AddMinutes(-1), Notes = "Segunda" },
                new DeliveryAttempt { Id = firstId, DeliveryId = delivery.Id, AttemptedAt = now.AddMinutes(-1) },
                new DeliveryAttempt { Id = Guid.NewGuid(), DeliveryId = otherDelivery.Id, AttemptedAt = now });
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync($"/api/deliveries/{delivery.Id}/attempts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var attempts = await response.Content.ReadFromJsonAsync<List<DeliveryAttemptResponse>>();
        Assert.NotNull(attempts);
        Assert.Equal(new[] { firstId, secondId, lastId }, attempts.Select(a => a.Id));
        Assert.All(attempts, a => Assert.Equal(delivery.Id, a.DeliveryId));
        Assert.Equal(new[] { now.AddMinutes(-1), now.AddMinutes(-1), now }, attempts.Select(a => a.AttemptedAt));
        Assert.Null(attempts[0].Notes);
        Assert.False(attempts[0].Successful);
        Assert.Equal("Entregue", attempts[2].Notes);
        Assert.True(attempts[2].Successful);
    }

    [Fact]
    public async Task GetAttempts_WhenDeliveryDoesNotExist_ReturnsNotFound()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync($"/api/deliveries/{Guid.NewGuid()}/attempts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateAttempt_WhenHistoryCannotBeSaved_RollsBackAttemptAndDeliveryChanges()
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var delivery = await CreateDeliveryAsync(client, "InTransit");
        var historyBefore = await GetHistoryAsync(client, delivery.Id);
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
            // Simulate a database failure during the successful attempt's SaveChanges.
            await dbContext.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER RejectDeliveredHistory
                BEFORE INSERT ON DeliveryStatusHistories
                WHEN NEW.NewStatus = 'Delivered'
                BEGIN
                    SELECT RAISE(ABORT, 'Simulated history persistence failure');
                END;
                """);
            var service = scope.ServiceProvider.GetRequiredService<DeliveryService>();

            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAttemptAsync(delivery.Id,
                new CreateDeliveryAttemptRequest { Successful = true }));
        }

        var persisted = await client.GetFromJsonAsync<DeliveryResponse>($"/api/deliveries/{delivery.Id}");
        Assert.NotNull(persisted);
        Assert.Equal("InTransit", persisted.Status);
        Assert.Null(persisted.DeliveredAt);
        Assert.Equal(delivery.UpdatedAt, persisted.UpdatedAt);
        var attempts = await client.GetFromJsonAsync<List<DeliveryAttemptResponse>>(
            $"/api/deliveries/{delivery.Id}/attempts");
        Assert.NotNull(attempts);
        Assert.Empty(attempts);
        var historyAfter = await GetHistoryAsync(client, delivery.Id);
        Assert.Equal(historyBefore.Select(h => h.Id), historyAfter.Select(h => h.Id));
    }

    private static async Task<DeliveryResponse> CreateDeliveryAsync(HttpClient client, string status = "Created")
    {
        using var created = await client.PostAsJsonAsync("/api/deliveries", new CreateDeliveryRequest
        {
            RecipientName = "Cliente DEL-013",
            OriginAddress = "Origem DEL-013",
            DestinationAddress = "Destino DEL-013"
        });
        created.EnsureSuccessStatusCode();
        var delivery = await created.Content.ReadFromJsonAsync<DeliveryResponse>();
        Assert.NotNull(delivery);

        if (status == "Delivered")
        {
            using var inTransit = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/status",
                new UpdateDeliveryStatusRequest { Status = "InTransit" });
            inTransit.EnsureSuccessStatusCode();
        }

        if (status != "Created")
        {
            using var updated = await client.PatchAsJsonAsync($"/api/deliveries/{delivery.Id}/status",
                new UpdateDeliveryStatusRequest { Status = status });
            updated.EnsureSuccessStatusCode();
            delivery = await updated.Content.ReadFromJsonAsync<DeliveryResponse>();
            Assert.NotNull(delivery);
        }

        return delivery;
    }

    private static async Task<List<DeliveryStatusHistoryResponse>> GetHistoryAsync(HttpClient client, Guid id)
    {
        var history = await client.GetFromJsonAsync<List<DeliveryStatusHistoryResponse>>(
            $"/api/deliveries/{id}/history");
        Assert.NotNull(history);
        return history;
    }
}
