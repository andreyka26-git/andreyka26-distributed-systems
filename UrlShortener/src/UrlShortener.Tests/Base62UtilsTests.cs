using UrlShortener.ShortUrlGeneration;
using Xunit;

namespace UrlShortener.Tests;

public class Base62UtilsTests
{
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(1L, "1")]
    [InlineData(62L, "10")]
    [InlineData(3844L, "100")]
    [InlineData(12345L, "3D7")]
    public void ToBase62_ShouldConvertLongToBase62_Correctly(long input, string expected)
    {
        // Act
        var result = Base62Utils.ToBase62(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToBase62_ShouldHandleLargeNumbers()
    {
        // Arrange
        var largeNumber = long.MaxValue;

        // Act
        var result = Base62Utils.ToBase62(largeNumber);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.True(result.Length > 1);
    }

    [Fact]
    public void ToBase62_WithByteArray_ShouldReturnValidString()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        // Act
        var result = Base62Utils.ToBase62(bytes);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void ToBase62_WithEmptyByteArray_ShouldReturnZero()
    {
        // Arrange
        var bytes = new byte[0];

        // Act
        var result = Base62Utils.ToBase62(bytes);

        // Assert
        Assert.Equal("0", result);
    }

    [Fact]
    public void ToBase62_WithNullByteArray_ShouldReturnZero()
    {
        // Arrange
        byte[]? bytes = null;

        // Act
        var result = Base62Utils.ToBase62(bytes!);

        // Assert
        Assert.Equal("0", result);
    }

    [Fact]
    public void ToBase62_WithNegativeNumber_ShouldThrowArgumentException()
    {
        // Arrange
        var negativeNumber = -123L;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Base62Utils.ToBase62(negativeNumber));
    }

    [Fact]
    public void ToBase62_ShouldOnlyUseValidBase62Characters()
    {
        // Arrange
        var validChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        var testNumbers = new long[] { 0, 1, 62, 3844, 12345, 9876543210 };

        foreach (var number in testNumbers)
        {
            // Act
            var result = Base62Utils.ToBase62(number);

            // Assert
            Assert.True(result.All(c => validChars.Contains(c)), 
                       $"Result '{result}' for input {number} contains invalid Base62 characters");
        }
    }
}