namespace DeliveryLab.Api.DTOs;

public class DeliveryAttemptResponse
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public bool Successful { get; set; }
    public string? Notes { get; set; }
}
