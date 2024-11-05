using Infra.Redis.Interfaces;
using Microsoft.AspNetCore.Mvc;
using WebApi.Boundaries.UseCases;

namespace WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private const string Prefix = "Test";

    private readonly ILogger<WeatherForecastController> _logger;


    public WeatherForecastController(ILogger<WeatherForecastController> logger)
    {
        _logger = logger;
    }


    [HttpGet("add-cache")]
    public async Task<IActionResult> AddAsync([FromServices] ICacheService cacheService, [FromServices] IGetWeatherForecastUseCase useCase)
    {
        var key = $"{Prefix}:1";

        var output = await cacheService.GetOrSetAsync(
            key,
            async () => await useCase.Get());


        return Ok(output);
    }
    
    [HttpGet("expire-cache")]
    public async Task<IActionResult> ExpireAsync([FromServices] ICacheService cacheService, [FromServices] IGetWeatherForecastUseCase useCase)
    {
        var key = $"{Prefix}:1";

        await cacheService.ExpireAsync(key);


        return Ok();
    }
}