using DeliveryLab.Api.DTOs;
using DeliveryLab.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeliveryLab.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriverController(DriverService driverService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<DriverResponse>> Create(CreateDriverRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Document))
        {
            return BadRequest("Name and Document are required.");
        }

        try
        {
            var driver = await driverService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = driver.Id },
                driver);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<DriverResponse>>> GetAll()
    {
        var drivers = await driverService.GetAllAsync();

        return Ok(drivers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DriverResponse>> GetById(Guid id)
    {
        var driver = await driverService.GetByIdAsync(id);

        if (driver is null)
        {
            return NotFound(new
            {
                message = "Driver not found."
            });
        }

        return Ok(driver);
    }
}