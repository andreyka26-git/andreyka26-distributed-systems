namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithGuid : IShortUrlGeneratorStrategy
{
    public Task<string> CreateShortUrlAsync(string originalUrl)
    {
        // Generate a new GUID and remove dashes
        var guid = Guid.NewGuid();
        var guidString = guid.ToString().Replace("-", "");
        
        // Convert the GUID string to bytes and then to Base62
        var guidBytes = System.Text.Encoding.UTF8.GetBytes(guidString);
        var shortUrl = Base62Utils.ToBase62(guidBytes);
        
        return Task.FromResult(shortUrl);
    }
}