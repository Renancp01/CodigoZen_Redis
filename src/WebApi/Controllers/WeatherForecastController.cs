using System.Diagnostics;
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
    public async Task<IActionResult> AddAsync([FromServices] ICacheService cacheService,
        [FromServices] IGetWeatherForecastUseCase useCase)
    {
        for (int i = 0; i < 200000; i++)
        {
            var key = $"{Prefix}:{i}";
            var output = await cacheService.GetOrSetAsync(
                key,
                async () => await useCase.Get());
        }

        return Ok();
    }

    [HttpGet("expire-cache")]
    public async Task<IActionResult> ExpireAsync([FromServices] ICacheService cacheService,
        [FromServices] IGetWeatherForecastUseCase useCase)
    {
        var key = $"{Prefix}:";

        var stopWatch = new Stopwatch();
        stopWatch.Start();
        await cacheService.ExpireByPrefixInBatchAsync(key);
        stopWatch.Stop();

        return Ok();
    }
}