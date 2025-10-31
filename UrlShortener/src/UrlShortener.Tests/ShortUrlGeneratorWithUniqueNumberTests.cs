using Moq;
using Microsoft.Extensions.Configuration;
using UrlShortener.ShortUrlGeneration;
using UrlShortener.UniqueNumberGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class ShortUrlGeneratorWithUniqueNumberTests
{
    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnValidShortUrl()
    {
        // Arrange
        var mockUniqueIdClient = new Mock<IUniqueIdClient>();
        mockUniqueIdClient.Setup(x => x.GetUniqueIdAsync())
                         .ReturnsAsync(12345L);

        var generator = new ShortUrlGeneratorWithUniqueNumber(mockUniqueIdClient.Object);
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
        mockUniqueIdClient.Verify(x => x.GetUniqueIdAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ShouldReturnDifferentUrls_ForDifferentIds()
    {
        // Arrange
        var mockUniqueIdClient1 = new Mock<IUniqueIdClient>();
        var mockUniqueIdClient2 = new Mock<IUniqueIdClient>();
        
        mockUniqueIdClient1.Setup(x => x.GetUniqueIdAsync()).ReturnsAsync(12345L);
        mockUniqueIdClient2.Setup(x => x.GetUniqueIdAsync()).ReturnsAsync(67890L);

        var generator1 = new ShortUrlGeneratorWithUniqueNumber(mockUniqueIdClient1.Object);
        var generator2 = new ShortUrlGeneratorWithUniqueNumber(mockUniqueIdClient2.Object);
        
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl1 = await generator1.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator2.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl1);
        Assert.NotNull(shortUrl2);
        Assert.NotEqual(shortUrl1, shortUrl2);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(12345L)]
    [InlineData(222111876543210L)]
    public async Task CreateShortUrlAsync_ShouldHandleVariousIds(long uniqueId)
    {
        // Arrange
        var mockUniqueIdClient = new Mock<IUniqueIdClient>();
        mockUniqueIdClient.Setup(x => x.GetUniqueIdAsync())
                         .ReturnsAsync(uniqueId);

        var generator = new ShortUrlGeneratorWithUniqueNumber(mockUniqueIdClient.Object);
        var originalUrl = "https://www.example.com";

        // Act
        var shortUrl = await generator.CreateShortUrlAsync(originalUrl);
        var shortUrl2 = await generator.CreateShortUrlAsync(originalUrl);

        // Assert
        Assert.NotNull(shortUrl);
        Assert.NotEmpty(shortUrl);
        Assert.Equal(shortUrl2, shortUrl);
    }
}