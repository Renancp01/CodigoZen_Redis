using System.Net;
using System.Text.Json;
using Infra.Redis.Configurations;
using Infra.Redis.Responses;
using Infra.Redis.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace UnitTests.Infra.Redis.Services;

public class CacheServiceTests
{
    private readonly Mock<IDatabase> _redisDatabaseMock;
    private readonly Mock<IServer> _redisServerMock;
    private readonly Mock<IConnectionMultiplexer> _redisMultiplexerMock;
    private readonly Mock<ILogger<CacheService>> _loggerMock;
    private readonly CacheConfiguration _cacheConfiguration;
    private readonly CacheService _cacheService;

    public CacheServiceTests()
    {
        _redisDatabaseMock = new Mock<IDatabase>();
        _redisServerMock = new Mock<IServer>();
        _redisMultiplexerMock = new Mock<IConnectionMultiplexer>();
        _loggerMock = new Mock<ILogger<CacheService>>();
        _cacheConfiguration = new CacheConfiguration { AbsoluteExpirationInMin = 60 };

        _redisMultiplexerMock.Setup(m => m.GetServer(It.IsAny<EndPoint>(), It.IsAny<object>()))
            .Returns(_redisServerMock.Object);
        _redisMultiplexerMock.Setup(m => m.GetEndPoints(It.IsAny<bool>()))
            .Returns(new EndPoint[] { new DnsEndPoint("localhost", 6379) });

        _redisDatabaseMock.SetupGet(db => db.Multiplexer)
            .Returns(_redisMultiplexerMock.Object);

        var mockOptions = new Mock<IOptionsMonitor<CacheConfiguration>>();
        mockOptions.Setup(o => o.CurrentValue).Returns(_cacheConfiguration);
        _cacheService = new CacheService(mockOptions.Object, _redisMultiplexerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task ExpireAsync_ShouldUpdateExpiration_WhenCacheItemExists()
    {
        // Arrange
        var key = "test_key";
        var response = new Response<dynamic> { Data = "test_data", ExpireAt = DateTime.UtcNow.AddMinutes(30) };
        var serializedResponse = JsonSerializer.Serialize(response);
        _redisDatabaseMock.Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>()))
            .ReturnsAsync(serializedResponse);

        // Act
        await _cacheService.ExpireAsync(key);

        // Assert
        _redisDatabaseMock.Verify(db => db.StringSetAsync(
            key,
            It.Is<RedisValue>(s =>
                JsonSerializer.Deserialize<Response<dynamic>>(s, (JsonSerializerOptions)null).ExpireAt <=
                DateTime.UtcNow),
            It.IsAny<TimeSpan>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()
        ), Times.Once);
    }

    [Fact]
    public async Task ExpireByPrefixAsync_ShouldUpdateExpiration_ForMatchingKeys()
    {
        // Arrange
        string prefix = "test:";
        var keys = new RedisKey[] { "test:key1", "test:key2" };

        _redisServerMock.Setup(s => s.Keys(It.IsAny<int>(), $"{prefix}*", It.IsAny<int>(), It.IsAny<long>(),
                It.IsAny<int>(), It.IsAny<CommandFlags>()))
            .Returns(keys);

        var cacheItems = new RedisValue[]
        {
            JsonSerializer.Serialize(new Response<dynamic>
                { Data = "value1", ExpireAt = DateTime.UtcNow.AddMinutes(30) }),
            JsonSerializer.Serialize(new Response<dynamic>
                { Data = "value2", ExpireAt = DateTime.UtcNow.AddMinutes(30) })
        };

        _redisDatabaseMock.Setup(db => db.StringGetAsync(keys, It.IsAny<CommandFlags>()))
            .ReturnsAsync(cacheItems);

        // Act
        await _cacheService.ExpireByPrefixAsync(prefix);

        // Assert
        _redisDatabaseMock.Verify(db => db.StringSetAsync(
            It.IsAny<RedisKey>(),
            It.IsAny<RedisValue>(),
            TimeSpan.FromMinutes(_cacheConfiguration.AbsoluteExpirationInMin),
            When.Always,
            It.IsAny<CommandFlags>()), Times.Exactly(2));

        // _loggerMock.Verify(log => log.LogInformation("Expiração atualizada para a chave: {Key}", It.IsAny<object[]>()), Times.Exactly(2));
    }
}