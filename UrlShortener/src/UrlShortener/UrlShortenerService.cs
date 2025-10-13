using System.Text;
using StackExchange.Redis;
using UrlShortener.ShortUrlGeneration;

namespace UrlShortener;

public class UrlShortenerService
{
    private readonly IDatabase _redis;
    private readonly IShortUrlGeneratorFactory _generatorFactory;

    public UrlShortenerService(IConnectionMultiplexer connectionMultiplexer, IShortUrlGeneratorFactory generatorFactory)
    {
        _redis = connectionMultiplexer.GetDatabase();
        _generatorFactory = generatorFactory;
    }

    public async Task<string> CreateShortUrlAsync(long uniqueNumber, string originalUrl)
    {
        var shortUrl = Base62Utils.ToBase62(uniqueNumber);
        await _redis.StringSetAsync(shortUrl, originalUrl);
        return shortUrl;
    }

    public async Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var strategy = _generatorFactory.CreateStrategy();
        var shortUrl = await strategy.CreateShortUrlAsync(originalUrl);
        await _redis.StringSetAsync(shortUrl, originalUrl);
        return shortUrl;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortUrl)
    {
        var originalUrl = await _redis.StringGetAsync(shortUrl);
        return originalUrl;
    }
}