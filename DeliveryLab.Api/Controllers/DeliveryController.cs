using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryLab.Api.Controllers;

[ApiController]
[Route("api/deliveries")]
public class DeliveryController(DeliveryService deliveryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeliveryResponse>> Create(CreateDeliveryRequest request)
    {
        var delivery = await deliveryService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = delivery.Id }, delivery);
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<DeliveryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeliveryResponse>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var validStatuses = new[]
        {
        "Created",
        "InTransit",
        "Delivered",
        "Cancelled"
    };

        if (!string.IsNullOrWhiteSpace(status) &&
            !validStatuses.Contains(status))
        {
            return BadRequest("Invalid status.");
        }

        if (page < 1)
        {
            return BadRequest("Page must be greater than or equal to 1.");
        }

        if (pageSize < 1)
        {
            return BadRequest("PageSize must be greater than or equal to 1.");
        }

        if (pageSize > 100)
        {
            return BadRequest("PageSize must be less than or equal to 100.");
        }

        var deliveries = await deliveryService.GetAllAsync(status, page, pageSize);

        return Ok(deliveries);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryResponse>> GetById(Guid id)
    {
        var delivery = await deliveryService.GetByIdAsync(id);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpGet("tracking/{trackingCode}")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryResponse>> GetByTrackingCode(string trackingCode)
    {
        var delivery = await deliveryService.GetByTrackingCodeAsync(trackingCode);
        return delivery is null ? NotFound() : Ok(delivery);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryResponse>> UpdateStatus(Guid id, UpdateDeliveryStatusRequest request)
    {
        try
        {
            var delivery = await deliveryService.UpdateStatusAsync(id, request);

            if (delivery is null)
            {
                return NotFound();
            }

            return Ok(delivery);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id:guid}/cancel")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryResponse>> Cancel(Guid id)
    {
        try
        {
            var request = new UpdateDeliveryStatusRequest 
            {
                Status = "Cancelled"
            };

            var delivery = await deliveryService.UpdateStatusAsync(id, request);

            if (delivery is null)
            {
                return NotFound();
            }

            return Ok(delivery);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPatch("{id:guid}/driver")]
    [ProducesResponseType(typeof(DeliveryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeliveryResponse>> AssignDriver(Guid id, AssignDriverRequest request)
    {
        try
        {
            var delivery = await deliveryService.AssignDriverAsync(id, request);

            if (delivery is null)
            {
                return NotFound();
            }

            return Ok(delivery);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<DeliveryStatusHistoryResponse>>> GetHistory(Guid id)
    {
        var delivery = await deliveryService.GetByIdAsync(id);

        if (delivery is null)
        {
            return NotFound();
        }

        var history = await deliveryService.GetHistoryAsync(id);

        return Ok(history);
    }
}
