using System.Text.Json;
using Contracts.Models;
using Infra.Redis.Configurations;
using Infra.Redis.Interfaces;
using Infra.Redis.Responses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Infra.Redis.Services;

public class CacheService : ICacheService
{
    private readonly CacheConfiguration _cacheConfiguration;
    private readonly IDatabase _redisDatabase;
    private readonly ILogger<CacheService> _logger;

    public CacheService(
        IOptionsMonitor<CacheConfiguration> cacheConfiguration,
        IConnectionMultiplexer redisConnection,
        ILogger<CacheService> logger)
    {
        _cacheConfiguration = cacheConfiguration.CurrentValue;
        _logger = logger;
        _redisDatabase = redisConnection.GetDatabase();
    }

    public async Task<T?> GetOrSetAsync<T>(string key, Func<Task<T>> func, bool disableCache = false,
        bool useExpiredCache = false)
    {
        try
        {
            if (disableCache)
            {
                _logger.LogInformation("Cache is disabled for key: {Key}", key);

                return await func().ConfigureAwait(false);
            }

            var cacheResponse = await GetValueFromCacheAsync<T>(key).ConfigureAwait(false);

            if (cacheResponse is { IsValid: true, Expired: false })
            {
                _logger.LogInformation("Cache hit for key: {Key}", key);

                return cacheResponse.Data;
            }

            var item = await func().ConfigureAwait(false);

            if (!ItemIsValid(item, key))
            {
                _logger.LogWarning("Invalid item for key: {Key}", key);

                return UseExpiredCache(item, cacheResponse, useExpiredCache, key);
            }

            await SetValueAsync(key, item).ConfigureAwait(false);

            _logger.LogInformation("Cache set for key: {Key}", key);
            return item;
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Redis connection exception for key: {Key}", key);
            return await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception for key: {Key}", key);
            return await func().ConfigureAwait(false);
        }
    }

    private async Task SetValueAsync<T>(string key, T item)
    {
        var expiry = TimeSpan.FromMinutes(_cacheConfiguration.AbsoluteExpirationInMin);

        var response = new Response<T>
        {
            Data = item,
            ExpireAt = DateTime.UtcNow.AddMinutes(_cacheConfiguration.CacheDurationInMin)
        };

        var serializedResponse = JsonSerializer.Serialize(response);

        await _redisDatabase.StringSetAsync(key, serializedResponse, expiry).ConfigureAwait(false);

        _logger.LogInformation("Value set in cache for key: {Key}", key);
    }

    private T? UseExpiredCache<T>(T item, Response<T>? cacheResult, bool useExpiredCache, string key)
    {
        if (useExpiredCache && cacheResult is { IsValid: true })
        {
            _logger.LogWarning("Returning expired cache for key: {Key}", key);
            return cacheResult.Data;
        }

        _logger.LogWarning("Returning new item for key: {Key}", key);

        return item;
    }

    private bool ItemIsValid<T>(T item, string key)
    {
        if (item is Result { IsValid: true })
        {
            _logger.LogInformation("Item is valid for key: {Key}", key);
            return true;
        }

        _logger.LogWarning("Item is invalid for key: {Key}", key);
        return false;
    }

    private async Task<Response<T>?> GetValueFromCacheAsync<T>(string key)
    {
        var cacheItem = await _redisDatabase.StringGetAsync(key).ConfigureAwait(false);

        if (cacheItem.HasValue)
        {
            var response = JsonSerializer.Deserialize<Response<T>>(cacheItem);
            return response;
        }

        _logger.LogInformation("Cache miss for key: {Key}", key);

        return default;
    }

    public async Task ExpireAsync(string key)
    {
        var cacheItem = await _redisDatabase.StringGetAsync(key).ConfigureAwait(false);

        if (cacheItem.HasValue)
        {
            var response = JsonSerializer.Deserialize<Response<dynamic>>(cacheItem);
            if (response != null)
            {
                response.ExpireAt = DateTime.UtcNow;

                var serializedResponse = JsonSerializer.Serialize(response);

                var expiry = TimeSpan.FromMinutes(_cacheConfiguration.AbsoluteExpirationInMin);

                await _redisDatabase.StringSetAsync(key, serializedResponse, expiry).ConfigureAwait(false);

                _logger.LogInformation("Expiration updated for key: {Key}", key);
            }
        }
        else
        {
            _logger.LogWarning("No cache item found for key: {Key}", key);
        }
    }
    
    public async Task ExpireByPrefixAsync(string prefix)
    {
        var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{prefix}*").ToArray();

        foreach (var key in keys)
        {
            var cacheItem = await _redisDatabase.StringGetAsync(key).ConfigureAwait(false);

            if (cacheItem.HasValue)
            {
                var response = JsonSerializer.Deserialize<Response<dynamic>>(cacheItem);
                if (response != null)
                {
                    response.ExpireAt = DateTime.UtcNow;

                    var serializedResponse = JsonSerializer.Serialize(response);

                    var expiry = TimeSpan.FromMinutes(_cacheConfiguration.AbsoluteExpirationInMin);

                    await _redisDatabase.StringSetAsync(key, serializedResponse, expiry).ConfigureAwait(false);

                    _logger.LogInformation("Expiration updated for key: {Key}", key);
                }
            }
            else
            {
                _logger.LogWarning("No cache item found for key: {Key}", key);
            }
        }
    }
    
    public async Task ExpireByPrefixInBatchAsync(string prefix)
    {
        var server = _redisDatabase.Multiplexer.GetServer(_redisDatabase.Multiplexer.GetEndPoints().First());
        var keys = server.Keys(pattern: $"{prefix}*").ToArray();

        if (keys.Length == 0)
        {
            _logger.LogInformation("Nenhuma chave encontrada com o prefixo: {Prefix}", prefix);
            return;
        }

        // Obter todos os itens de cache em uma única operação em lote
        var cacheItems = await _redisDatabase.StringGetAsync(keys).ConfigureAwait(false);

        var expiry = TimeSpan.FromMinutes(_cacheConfiguration.AbsoluteExpirationInMin);

        // Criar um batch para executar múltiplas operações de uma vez
        var batch = _redisDatabase.CreateBatch();
        var tasks = new List<Task>();

        for (int i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            var cacheItem = cacheItems[i];

            if (cacheItem.HasValue)
            {
                var response = JsonSerializer.Deserialize<Response<dynamic>>(cacheItem);
                if (response != null)
                {
                    response.ExpireAt = DateTime.UtcNow;
                    var serializedResponse = JsonSerializer.Serialize(response);

                    // Enfileirar a operação StringSetAsync no batch
                    var task = batch.StringSetAsync(key, serializedResponse, expiry);
                    tasks.Add(task);

                    _logger.LogInformation("Expiração atualizada para a chave: {Key}", key);
                }
            }
            else
            {
                _logger.LogWarning("Nenhum item de cache encontrado para a chave: {Key}", key);
            }
        }

        // Executar todas as operações enfileiradas no batch
        batch.Execute();

        // Aguardar todas as tarefas para garantir a conclusão
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}