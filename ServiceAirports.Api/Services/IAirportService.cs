using ServiceAirports.Api.Models;

namespace ServiceAirports.Api.Services;

public interface IAirportService
{
    Task<List<Airport>> GetAllAirportsAsync();
}
