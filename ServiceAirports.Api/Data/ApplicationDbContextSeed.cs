using Microsoft.EntityFrameworkCore;
using ServiceAirports.Api.Models;

namespace ServiceAirports.Api.Data;

public static class ApplicationDbContextSeed
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.Airports.AnyAsync())
        {
            return;
        }

        var airports = new List<Airport>
        {
            new()
            {
                Name = "London Heathrow Airport",
                Code = "LHR",
                City = "London",
                Country = "United Kingdom"
            },
            new()
            {
                Name = "John F. Kennedy International Airport",
                Code = "JFK",
                City = "New York",
                Country = "United States"
            },
            new()
            {
                Name = "Dubai International Airport",
                Code = "DXB",
                City = "Dubai",
                Country = "United Arab Emirates"
            }
        };

        await context.Airports.AddRangeAsync(airports);
        await context.SaveChangesAsync();
    }
}
