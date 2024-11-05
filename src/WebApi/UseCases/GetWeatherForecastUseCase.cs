using Contracts.Models;
using WebApi.Boundaries.UseCases;

namespace WebApi.UseCases;

public class GetWeatherForecastUseCase : IGetWeatherForecastUseCase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };
    
    public async Task<Result<WeatherForecast>> Get()
    {
        await Task.CompletedTask;
        return new Result<WeatherForecast>(new WeatherForecast()
        {
            Date = DateTime.UtcNow,
            TemperatureC = 10,
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        });
    }
}