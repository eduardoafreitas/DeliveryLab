namespace DeliveryLab.Api.DTOs
{
    public class DeliveryStatusHistoryResponse
    {
        public Guid Id { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
    }
}
