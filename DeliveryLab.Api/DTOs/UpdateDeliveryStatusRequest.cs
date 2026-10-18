using System.ComponentModel.DataAnnotations;

namespace DeliveryLab.Api.DTOs;

public class UpdateDeliveryStatusRequest
{
    [Required(ErrorMessage = "Informe o status.")]
    [RegularExpression("^(Created|InTransit|Delivered|Cancelled)$",
        ErrorMessage = "Status deve ser Created, InTransit, Delivered ou Cancelled.")]
    public string Status { get; set; } = string.Empty;
}
