using System.Security.Cryptography;
using System.Text;

namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithHashing : IShortUrlGeneratorStrategy
{
    public Task<string> CreateShortUrlAsync(string originalUrl)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(originalUrl));

        var shortUrl = Base62Utils.ToBase62(hashBytes);
        
        return Task.FromResult(shortUrl);
    }
}