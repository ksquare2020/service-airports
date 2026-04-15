using Microsoft.EntityFrameworkCore;
using ServiceAirports.Api.Data;
using ServiceAirports.Api.Models;

namespace ServiceAirports.Api.Services;

public class AirportService(ApplicationDbContext dbContext, IConfiguration configuration) : IAirportService
{
    private readonly List<Airport> staticAirports =
    [
        new() { Name = "London Heathrow Airport", Code = "LHR", City = "London", Country = "United Kingdom" },
        new() { Name = "Chennai International Airport", Code = "MAA", City = "Chennai", Country = "India" },
        new() { Name = "John F. Kennedy International Airport", Code = "JFK", City = "New York", Country = "United States" },
        new() { Name = "Dubai International Airport", Code = "DXB", City = "Dubai", Country = "United Arab Emirates" },
        new() { Name = "Singapore Changi Airport", Code = "SIN", City = "Singapore", Country = "Singapore" },
        new() { Name = "Sydney Airport", Code = "SYD", City = "Sydney", Country = "Australia" }
    ];

    public async Task<List<Airport>> GetAllAirportsAsync()
    {
        var useDatabase = configuration.GetValue<bool>("DataSource:UseDatabase");

        if (!useDatabase)
        {
            return staticAirports
                .OrderBy(airport => airport.Name)
                .ToList();
        }

        return await dbContext.Airports
            .AsNoTracking()
            .OrderBy(airport => airport.Name)
            .ToListAsync();
    }
}
