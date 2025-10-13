namespace UrlShortener.ShortUrlGeneration;

public class ShortUrlGeneratorWithUniqueNumber : IShortUrlGeneratorStrategy
{
    private readonly IUniqueIdClient _uniqueIdClient;

    public ShortUrlGeneratorWithUniqueNumber(IUniqueIdClient uniqueIdClient)
    {
        _uniqueIdClient = uniqueIdClient;
    }

    public async Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var id = await _uniqueIdClient.GetUniqueIdAsync();
        var shortUrl = Base62Utils.ToBase62(id);
        return shortUrl;
    }
}