using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class ShortUrlGeneratorWithGuidTests
{
    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnValidShortUrl()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithGuid();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnDifferentUrls_ForMultipleCalls()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithGuid();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.NotEqual(shortUrl1, shortUrl2); // GUIDs should be unique
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://www.example.com")]
    [InlineData("https://very-long-url-that-contains-many-characters.com/path/to/resource?param=value")]
    public async Task CreateShortUrlAsync_ShouldGenerateUrlForAnyInput(string originalUrl)
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithGuid();

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldGenerateUniqueUrls_ForMultipleInstances()
    {
        // Arrange
        var generator1 = new ShortUrlGeneratorWithGuid();
        var generator2 = new ShortUrlGeneratorWithGuid();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator1.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator2.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.NotEqual(shortUrl1, shortUrl2); // Different instances should generate different GUIDs
    }
}