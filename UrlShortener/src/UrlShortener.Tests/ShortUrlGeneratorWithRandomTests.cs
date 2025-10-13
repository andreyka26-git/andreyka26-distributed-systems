using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class ShortUrlGeneratorWithRandomTests
{
    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnValidShortUrl()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
        Assert.Equal(7, shortUrl.Length);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnDifferentUrls_ForMultipleCalls()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator.CreateShortUrlAsync(originalUrl);
        var shortUrl3 = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.NotNull(shortUrl3);
        Assert.NotEqual(shortUrl1, shortUrl2);
        Assert.NotEqual(shortUrl1, shortUrl3);
        Assert.NotEqual(shortUrl2, shortUrl3);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldAlwaysReturn7Characters()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();
        var testUrls = new[]
        {
            "https://www.example.com",
            "",
            "https://very-long-url-that-contains-many-characters.com/path/to/resource?param=value",
            "short.url"
        };

        foreach (var url in testUrls)
        {
            // Act
            var shortUrl = await generator.CreateShortUrlAsync(url);

            // Assert
            Assert.Equal(7, shortUrl.Length);
        }
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldOnlyUseValidBase62Characters()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();
        var validChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var originalUrl = "https://www.example.com";

        // Act & Assert
        for (int i = 0; i < 10; i++) // Test multiple times due to randomness
        {
            var shortUrl = await generator.CreateShortUrlAsync(originalUrl);
            Assert.True(shortUrl.All(c => validChars.Contains(c)), 
                       $"Short URL '{shortUrl}' contains invalid Base62 characters");
        }
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldGenerateUniqueUrls_WithHighProbability()
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();
        var originalUrl = "https://www.example.com";
        var generatedUrls = new HashSet<string>();
        var numberOfTests = 100;

        // Act
        for (int i = 0; i < numberOfTests; i++)
        {
            var shortUrl = await generator.CreateShortUrlAsync(originalUrl);
            generatedUrls.Add(shortUrl);
        }

        // Assert
        // With 7 characters and 62 possible characters per position, 
        // the probability of collision in 100 attempts should be very low
        // We expect at least 95% unique URLs (allowing for some rare collisions)
        var uniqueCount = generatedUrls.Count;
        var uniquenessRatio = (double)uniqueCount / numberOfTests;
        
        Assert.True(uniquenessRatio >= 0.95, 
                   $"Expected at least 95% unique URLs, but got {uniquenessRatio:P2} ({uniqueCount}/{numberOfTests})");
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldWorkWithDifferentInstances()
    {
        // Arrange
        var generator1 = new ShortUrlGeneratorWithRandom();
        var generator2 = new ShortUrlGeneratorWithRandom();
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator1.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator2.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.Equal(7, shortUrl1.Length);
        Assert.Equal(7, shortUrl2.Length);
        // Different instances should likely generate different URLs
        // (though not guaranteed due to randomness)
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://www.example.com")]
    [InlineData("https://very-long-url.com/with/many/path/segments?param1=value1&param2=value2")]
    [InlineData("ftp://files.example.com/file.txt")]
    [InlineData("mailto:test@example.com")]
    public async Task CreateShortUrlAsync_ShouldHandleVariousInputs(string originalUrl)
    {
        // Arrange
        var generator = new ShortUrlGeneratorWithRandom();

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.Equal(7, shortUrl.Length);
        
        var validChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        Assert.True(shortUrl.All(c => validChars.Contains(c)));
    }
}