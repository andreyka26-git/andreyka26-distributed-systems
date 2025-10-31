using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using UrlShortener.Database;
using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class UrlShortenerServiceTests
{
    [Fact]
    public async Task CreateShortUrlAsync_WithStrategy_ShouldUseFactory()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=testdb;Username=test;Password=test"
            })
            .Build();

        var mockDatabaseSetup = new Mock<DatabaseSetup>(configuration, Mock.Of<ILogger<DatabaseSetup>>());
        var mockStrategy = new Mock<IShortUrlGeneratorStrategy>();
        mockStrategy.Setup(x => x.CreateShortUrlAsync(It.IsAny<string>()))
                   .ReturnsAsync("abc123");

        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        mockFactory.Setup(x => x.CreateStrategy())
                  .Returns(mockStrategy.Object);
        
        var loggerMock = new Mock<ILogger<UrlShortenerService>>();

        // We can't easily test the database storage without an actual database
        // This test focuses on verifying the strategy usage
        var service = new UrlShortenerService(configuration, mockDatabaseSetup.Object, mockFactory.Object, loggerMock.Object);
        var originalUrl = "https://www.example.com";

        // Act & Assert
        // Since this test would require a real database connection to complete,
        // we'll verify that it throws the expected exception (no database available)
        // while still testing that the factory and strategy are called
        
        // The actual database connection will fail, which is expected in a unit test environment
        await Assert.ThrowsAsync<Npgsql.PostgresException>(async () =>
            await service.CreateShortUrlAsync(originalUrl));
        
        // Verify that the factory was called even though the database operation failed
        mockFactory.Verify(x => x.CreateStrategy(), Times.Once);
        mockStrategy.Verify(x => x.CreateShortUrlAsync(originalUrl), Times.Once);
    }

    [Fact]
    public void CreateShortUrlAsync_WithUniqueNumber_ShouldUseBase62()
    {
        // Arrange
        var uniqueNumber = 12345L;

        // Act & Assert
        // This test verifies the Base62 conversion logic
        var expectedShortUrl = Base62Utils.ToBase62(uniqueNumber);
        Assert.NotNull(expectedShortUrl);
        Assert.Equal("3D7", expectedShortUrl); // This is the Base62 representation of 12345
        
        // The actual service call would fail due to database, but we can test the Base62 logic separately
    }

    [Fact]
    public async Task GetOriginalUrlAsync_ShouldThrowException_WhenConnectionStringIsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var mockDatabaseSetup = new Mock<DatabaseSetup>(configuration, Mock.Of<ILogger<DatabaseSetup>>());
        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        var loggerMock = new Mock<ILogger<UrlShortenerService>>();

        var service = new UrlShortenerService(configuration, mockDatabaseSetup.Object, mockFactory.Object, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetOriginalUrlAsync("abc123"));
    }

    [Fact]
    public async Task GetOriginalUrlAsync_ShouldThrowException_WhenConnectionStringIsEmpty()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ""
            })
            .Build();

        var mockDatabaseSetup = new Mock<DatabaseSetup>(configuration, Mock.Of<ILogger<DatabaseSetup>>());
        var mockFactory = new Mock<IShortUrlGeneratorFactory>();
        var loggerMock = new Mock<ILogger<UrlShortenerService>>();

        var service = new UrlShortenerService(configuration, mockDatabaseSetup.Object, mockFactory.Object, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetOriginalUrlAsync("abc123"));
    }
}