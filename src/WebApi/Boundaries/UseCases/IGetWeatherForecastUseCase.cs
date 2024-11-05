using Contracts.Models;

namespace WebApi.Boundaries.UseCases;

public interface IGetWeatherForecastUseCase
{
    Task<Result<WeatherForecast>> Get();
}