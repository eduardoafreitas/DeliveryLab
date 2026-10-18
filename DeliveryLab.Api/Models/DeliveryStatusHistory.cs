namespace DeliveryLab.Api.Models
{
    public class DeliveryStatusHistory
    {
        public Guid Id { get; set; }
        public Guid DeliveryId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}
