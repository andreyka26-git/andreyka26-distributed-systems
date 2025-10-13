using Microsoft.Extensions.Configuration;
using Moq;
using StackExchange.Redis;
using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class UrlShortenerServiceTests
{
    [Fact]
    public async Task CreateShortUrlAsync_WithStrategy_ShouldUseFactoryAndStoreInRedis()
    {
        // Arrange
        var mockRedis = new Mock<IDatabase>();
        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                                .Returns(mockRedis.Object);

        var mockStrategy = new Mock<IShortUrlGeneratorStrategy>();
        mockStrategy.Setup(x => x.CreateShortUrlAsync(It.IsAny<string>()))
                   .ReturnsAsync("abc123");

        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        mockFactory.Setup(x => x.CreateStrategy())
                  .Returns(mockStrategy.Object);

        var service = new UrlShortenerService(mockConnectionMultiplexer.Object, mockFactory.Object);
        var originalUrl = "https://www.example.com";

        // Act
        var result = await service.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.Equal("abc123", result);
        mockFactory.Verify(x => x.CreateStrategy(), Times.Once);
        mockStrategy.Verify(x => x.CreateShortUrlAsync(originalUrl), Times.Once);
        mockRedis.Verify(x => x.StringSetAsync("abc123", originalUrl, null, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task CreateShortUrlAsync_WithUniqueNumber_ShouldUseBase62AndStoreInRedis()
    {
        // Arrange
        var mockRedis = new Mock<IDatabase>();
        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                                .Returns(mockRedis.Object);

        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        
        var service = new UrlShortenerService(mockConnectionMultiplexer.Object, mockFactory.Object);
        var originalUrl = "https://www.example.com";
        var uniqueNumber = 12345L;

        // Act
        var result = await service.CreateShortUrlAsync(uniqueNumber, originalUrl);

        // Assert
        var expectedShortUrl = Base62Utils.ToBase62(uniqueNumber);
        Assert.Equal(expectedShortUrl, result);
        mockRedis.Verify(x => x.StringSetAsync(expectedShortUrl, originalUrl, null, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_ShouldReturnStoredUrl()
    {
        // Arrange
        var mockRedis = new Mock<IDatabase>();
        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                                .Returns(mockRedis.Object);

        var expectedUrl = "https://www.example.com";
        mockRedis.Setup(x => x.StringGetAsync("abc123", It.IsAny<CommandFlags>()))
                .ReturnsAsync(expectedUrl);

        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        var service = new UrlShortenerService(mockConnectionMultiplexer.Object, mockFactory.Object);

        // Act
        var result = await service.GetOriginalUrlAsync("abc123");

        // Assert
        Assert.Equal(expectedUrl, result);
        mockRedis.Verify(x => x.StringGetAsync("abc123", It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_ShouldReturnNull_WhenUrlNotFound()
    {
        // Arrange
        var mockRedis = new Mock<IDatabase>();
        var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                                .Returns(mockRedis.Object);

        mockRedis.Setup(x => x.StringGetAsync("nonexistent", It.IsAny<CommandFlags>()))
                .ReturnsAsync((RedisValue)RedisValue.Null);

        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        var service = new UrlShortenerService(mockConnectionMultiplexer.Object, mockFactory.Object);

        // Act
        var result = await service.GetOriginalUrlAsync("nonexistent");

        // Assert
        Assert.Null(result);
        mockRedis.Verify(x => x.StringGetAsync("nonexistent", It.IsAny<CommandFlags>()), Times.Once);
    }
}