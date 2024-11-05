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
    private readonly Mock<IDatabase> _mockDatabase;
    private readonly Mock<ILogger<CacheService>> _mockLogger;
    private readonly CacheService _cacheService;
    private readonly CacheConfiguration _cacheConfiguration;

    public CacheServiceTests()
    {
        _mockDatabase = new Mock<IDatabase>();
        _mockLogger = new Mock<ILogger<CacheService>>();
        _cacheConfiguration = new CacheConfiguration { AbsoluteExpirationInMin = 60 };
        var mockOptions = new Mock<IOptionsMonitor<CacheConfiguration>>();
        mockOptions.Setup(o => o.CurrentValue).Returns(_cacheConfiguration);

        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer.Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockDatabase.Object);

        _cacheService = new CacheService(mockOptions.Object, mockConnectionMultiplexer.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ExpireAsync_ShouldUpdateExpiration_WhenCacheItemExists()
    {
        // Arrange
        var key = "test_key";
        var response = new Response<dynamic> { Data = "test_data", ExpireAt = DateTime.UtcNow.AddMinutes(30) };
        var serializedResponse = JsonSerializer.Serialize(response);
        _mockDatabase.Setup(db => db.StringGetAsync(key, It.IsAny<CommandFlags>())).ReturnsAsync(serializedResponse);

        // Act
        await _cacheService.ExpireAsync(key);

        // Assert
        _mockDatabase.Verify(db => db.StringSetAsync(
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
}