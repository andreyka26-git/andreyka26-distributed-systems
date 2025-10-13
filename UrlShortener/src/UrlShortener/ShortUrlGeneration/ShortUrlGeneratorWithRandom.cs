namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithRandom : IShortUrlGeneratorStrategy
{
    private const string Base62Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int RandomStringLength = 7;
    private readonly Random _random;

    public ShortUrlGeneratorWithRandom()
    {
        _random = new Random();
    }

    public Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var randomString = GenerateRandomBase62String(RandomStringLength);
        return Task.FromResult(randomString);
    }

    private string GenerateRandomBase62String(int length)
    {
        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            chars[i] = Base62Characters[_random.Next(Base62Characters.Length)];
        }
        return new string(chars);
    }
}