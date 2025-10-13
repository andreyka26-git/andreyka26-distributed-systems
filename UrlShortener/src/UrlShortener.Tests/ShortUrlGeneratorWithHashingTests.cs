using Moq;
using StackExchange.Redis;
using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class ShortUrlGeneratorWithHashingTests
{
    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnConsistentResult_ForSameInput()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithHashing();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.Equal(shortUrl1, shortUrl2); // Same input should produce same hash
        Assert.NotEmpty(shortUrl1);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnDifferentResults_ForDifferentInputs()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithHashing();
        var originalUrl1 = "https://www.example.com";
        var originalUrl2 = "https://www.google.com";

        // Act
        var shortUrl1 = await generator.CreateShortUrlAsync(originalUrl1);
        var shortUrl2 = await generator.CreateShortUrlAsync(originalUrl2);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.NotEqual(shortUrl1, shortUrl2); // Different inputs should produce different hashes
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://www.example.com")]
    [InlineData("https://very-long-url-that-contains-many-characters-to-test-hash-generation.com/path/to/resource?param=value")]
    public async Task CreateShortUrlAsync_ShouldHandleVariousInputs(string originalUrl)
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithHashing();

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
    }
}