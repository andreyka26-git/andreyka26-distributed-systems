using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using UrlShortener;
using UrlShortener.Database;
using UrlShortener.UniqueNumberGeneration;

namespace UrlShortener.Tests;

public class AutoIncrementUniqueIdClientTests
{
    [Fact]
    public async Task GetUniqueIdAsync_ShouldThrowException_WhenConnectionStringIsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        
        var databaseSetupMock = new Mock<DatabaseSetup>(configuration, Mock.Of<ILogger<DatabaseSetup>>());
        var loggerMock = new Mock<ILogger<AutoIncrementUniqueIdClient>>();

        var client = new AutoIncrementUniqueIdClient(configuration, databaseSetupMock.Object, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUniqueIdAsync());
    }

    [Fact]
    public async Task GetUniqueIdAsync_ShouldThrowException_WhenConnectionStringIsEmpty()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ""
            })
            .Build();
        
        var databaseSetupMock = new Mock<DatabaseSetup>(configuration, Mock.Of<ILogger<DatabaseSetup>>());
        var loggerMock = new Mock<ILogger<AutoIncrementUniqueIdClient>>();

        var client = new AutoIncrementUniqueIdClient(configuration, databaseSetupMock.Object, loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUniqueIdAsync());
    }

    // Note: Integration tests with actual database would require a test database setup
    // and are typically run separately from unit tests
}