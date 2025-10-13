using Microsoft.Extensions.Configuration;
using Moq;
using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class ShortUrlGeneratorFactoryTests
{
    [Theory]
    [InlineData("Hashing", typeof(ShortUrlGeneratorWithHashing))]
    [InlineData("HASHING", typeof(ShortUrlGeneratorWithHashing))]
    [InlineData("hashing", typeof(ShortUrlGeneratorWithHashing))]
    [InlineData("UniqueNumber", typeof(ShortUrlGeneratorWithUniqueNumber))]
    [InlineData("UNIQUENUMBER", typeof(ShortUrlGeneratorWithUniqueNumber))]
    [InlineData("uniquenumber", typeof(ShortUrlGeneratorWithUniqueNumber))]
    [InlineData("Guid", typeof(ShortUrlGeneratorWithGuid))]
    [InlineData("GUID", typeof(ShortUrlGeneratorWithGuid))]
    [InlineData("guid", typeof(ShortUrlGeneratorWithGuid))]
    [InlineData("Random", typeof(ShortUrlGeneratorWithRandom))]
    [InlineData("RANDOM", typeof(ShortUrlGeneratorWithRandom))]
    [InlineData("random", typeof(ShortUrlGeneratorWithRandom))]
    [InlineData("Base62", typeof(ShortUrlGeneratorWithBase62))]
    [InlineData("BASE62", typeof(ShortUrlGeneratorWithBase62))]
    [InlineData("base62", typeof(ShortUrlGeneratorWithBase62))]
    public void CreateStrategy_ShouldReturnCorrectStrategy_BasedOnConfiguration(string configValue, Type expectedType)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ShortUrlGenerator:Strategy"] = configValue
            })
            .Build();

        var mockUniqueIdClient = new Mock<IUniqueIdClient>();
        var factory = new ShortUrlGeneratorFactory(configuration, mockUniqueIdClient.Object);

        // Act
        var strategy = factory.CreateStrategy();

        // Assert
        Assert.IsType(expectedType, strategy);
    }

    [Fact]
    public void CreateStrategy_ShouldReturnDefaultStrategy_WhenConfigurationIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>())
            .Build();

        var mockUniqueIdClient = new Mock<IUniqueIdClient>();
        var factory = new ShortUrlGeneratorFactory(configuration, mockUniqueIdClient.Object);

        // Act
        var strategy = factory.CreateStrategy();

        // Assert
        Assert.IsType<ShortUrlGeneratorWithUniqueNumber>(strategy);
    }

    [Theory]
    [InlineData("InvalidStrategy")]
    [InlineData("")]
    [InlineData("SomeRandomValue")]
    public void CreateStrategy_ShouldReturnDefaultStrategy_WhenConfigurationIsInvalid(string configValue)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ShortUrlGenerator:Strategy"] = configValue
            })
            .Build();

        var mockUniqueIdClient = new Mock<IUniqueIdClient>();
        var factory = new ShortUrlGeneratorFactory(configuration, mockUniqueIdClient.Object);

        // Act
        var strategy = factory.CreateStrategy();

        // Assert
        Assert.IsType<ShortUrlGeneratorWithUniqueNumber>(strategy);
    }
}