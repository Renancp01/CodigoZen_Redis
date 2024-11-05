using Infra.Redis.Configurations;
using Infra.Redis.Interfaces;
using Infra.Redis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Infra.Redis.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCache(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConfiguration = configuration
            .GetSection($"{nameof(RedisConfiguration)}")
            .Get<RedisConfiguration>();

        services.Configure<RedisConfiguration>(options =>
                configuration.GetSection($"{nameof(CacheConfiguration)}").Bind(options))
            .Configure<CacheConfiguration>(options =>
                configuration.GetSection($"{nameof(CacheConfiguration)}").Bind(options));

        var configurationOptions = ConfigurationOptions.Parse(redisConfiguration.ConnectionString);
        configurationOptions.ConnectTimeout = 5000;
        configurationOptions.SyncTimeout = 5000;
        configurationOptions.AsyncTimeout = 5000;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(configurationOptions));
        services.AddSingleton<ICacheService, CacheService>();
        return services;
    }
}