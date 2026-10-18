using System.ComponentModel.DataAnnotations;

namespace DeliveryLab.Api.DTOs;

public class CreateDeliveryRequest
{
    [Required(ErrorMessage = "Informe o nome do destinatário.")]
    public string RecipientName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço de origem.")]
    public string OriginAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço de destino.")]
    public string DestinationAddress { get; set; } = string.Empty;

    public DateOnly? EstimatedDeliveryDate { get; set; }
}
