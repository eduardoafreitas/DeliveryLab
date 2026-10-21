using System.Net;
using System.Net.Http.Json;
using DeliveryLab.Api.Data;
using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeliveryLab.Tests;

public class DeliveryPaginationDebuggingTests
{
    [Theory]
    [InlineData(int.MaxValue, 100)]
    [InlineData(1073741825, 4)]
    [InlineData(1073741825, 2)]
    [InlineData(int.MaxValue, 1)]
    [Trait("Task", "DEL-012")]
    public async Task GetAll_WhenPageIsExtremelyDistant_ReturnsEmptyPage(int page, int pageSize)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        // Keep enough deliveries in the isolated database to make the defect visible.
        for (var index = 1; index <= 3; index++)
        {
            using var created = await client.PostAsJsonAsync("/api/deliveries",
                new CreateDeliveryRequest
                {
                    RecipientName = $"Cliente DEL-012 {index}",
                    OriginAddress = "Origem DEL-012",
                    DestinationAddress = "Destino DEL-012"
                });

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var firstPage = await client.GetFromJsonAsync<List<DeliveryResponse>>(
            "/api/deliveries?page=1&pageSize=10");
        Assert.NotNull(firstPage);
        Assert.InRange(firstPage.Count, 3, 10);

        using var response = await client.GetAsync(
            $"/api/deliveries?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var deliveries = await response.Content.ReadFromJsonAsync<List<DeliveryResponse>>();
        Assert.NotNull(deliveries);

        // This distant page should contain no deliveries from this small dataset.
        Assert.Empty(deliveries);
    }

    [Theory]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [Trait("Task", "DEL-012")]
    public async Task GetAll_WhenPaginationIsInvalid_ReturnsBadRequest(int page, int pageSize)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();

        using var response = await client.GetAsync(
            $"/api/deliveries?page={page}&pageSize={pageSize}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("InTransit")]
    [Trait("Task", "DEL-012")]
    public async Task GetAll_WhenPaginationIsNormal_PreservesFilteringAndOrdering(string? status)
    {
        using var factory = new DeliveryApiFactory();
        using var client = factory.CreateClientWithDatabase();
        var date = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        var tiedFirstId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var tiedSecondId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var newestInTransitId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
        var oldestInTransitId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");
        var deliveredId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000005");

        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DeliveryLabDbContext>();
            var deliveries = new[]
            {
                new Delivery { Id = tiedSecondId, Status = "InTransit", CreatedAt = date },
                new Delivery { Id = tiedFirstId, Status = "InTransit", CreatedAt = date },
                new Delivery { Id = newestInTransitId, Status = "InTransit", CreatedAt = date.AddDays(1) },
                new Delivery { Id = oldestInTransitId, Status = "InTransit", CreatedAt = date.AddDays(-1) },
                new Delivery { Id = deliveredId, Status = "Delivered", CreatedAt = date.AddDays(2) }
            };

            foreach (var delivery in deliveries)
            {
                delivery.TrackingCode = $"DL-{delivery.Id:N}";
                delivery.RecipientName = "Cliente DEL-012";
                delivery.OriginAddress = "Origem DEL-012";
                delivery.DestinationAddress = "Destino DEL-012";
            }

            dbContext.Deliveries.AddRange(deliveries);
            await dbContext.SaveChangesAsync();
        }

        var filter = status is null ? string.Empty : $"&status={status}";
        var firstPage = await client.GetFromJsonAsync<List<DeliveryResponse>>(
            $"/api/deliveries?page=1&pageSize=2{filter}");
        var secondPage = await client.GetFromJsonAsync<List<DeliveryResponse>>(
            $"/api/deliveries?page=2&pageSize=2{filter}");

        Assert.NotNull(firstPage);
        Assert.NotNull(secondPage);
        var expectedFirstPage = status is null
            ? new[] { deliveredId, newestInTransitId }
            : new[] { newestInTransitId, tiedFirstId };
        var expectedSecondPage = status is null
            ? new[] { tiedFirstId, tiedSecondId }
            : new[] { tiedSecondId, oldestInTransitId };
        Assert.Equal(expectedFirstPage, firstPage.Select(delivery => delivery.Id));
        Assert.Equal(expectedSecondPage, secondPage.Select(delivery => delivery.Id));

        if (status is not null)
        {
            Assert.All(firstPage.Concat(secondPage), delivery => Assert.Equal(status, delivery.Status));
        }
    }
}
