namespace DeliveryLab.Api.Models;

public class DeliveryAttempt
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public DateTime AttemptedAt { get; set; }
    public bool Successful { get; set; }
    public string? Notes { get; set; }
}
