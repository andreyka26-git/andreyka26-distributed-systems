namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithGuid : IShortUrlGeneratorStrategy
{
    public Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var guid = Guid.NewGuid();
        var guidBytes = guid.ToByteArray();
        
        var shortUrl = Base62Utils.ToBase62(guidBytes);
        
        return Task.FromResult(shortUrl);
    }
}