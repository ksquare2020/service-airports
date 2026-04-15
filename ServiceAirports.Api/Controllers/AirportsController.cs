using Microsoft.AspNetCore.Mvc;
using ServiceAirports.Api.Services;

namespace ServiceAirports.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AirportsController(IAirportService airportService) : ControllerBase
{
    [HttpGet("getallairports")]
    public async Task<IActionResult> GetAllAirports()
    {
        try
        {
            var airports = await airportService.GetAllAirportsAsync();
            return Ok(airports);
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Unable to retrieve airport data."
            });
        }
    }
}
