using System.Text;

namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithBase62 : IShortUrlGeneratorStrategy
{
    public Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var urlBytes = Encoding.UTF8.GetBytes(originalUrl);
        var shortUrl = Base62Utils.ToBase62(urlBytes);
        
        return Task.FromResult(shortUrl);
    }
}