namespace DeliveryLab.Api.DTOs;

public class DeliveryResponse
{
    public Guid Id { get; set; }
    public string TrackingCode { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string OriginAddress { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateOnly? EstimatedDeliveryDate { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
