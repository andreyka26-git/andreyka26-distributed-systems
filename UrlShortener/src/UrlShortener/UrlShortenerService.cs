using System.Text;
using StackExchange.Redis;

namespace UrlShortener;

public class UrlShortenerService
{
    private const string Base62Characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private readonly IDatabase _redis;

    public UrlShortenerService(IConnectionMultiplexer connectionMultiplexer)
    {
        _redis = connectionMultiplexer.GetDatabase();
    }

    public async Task<string> CreateShortUrlAsync(long uniqueNumber, string originalUrl)
    {
        var shortUrl = ToBase62(uniqueNumber);
        await _redis.StringSetAsync(shortUrl, originalUrl);
        return shortUrl;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortUrl)
    {
        var originalUrl = await _redis.StringGetAsync(shortUrl);
        return originalUrl;
    }

    public string ToBase62(long value)
    {
        if (value == 0)
        {
            return "0";
        }

        var base62 = new StringBuilder();
        while (value > 0)
        {
            base62.Insert(0, Base62Characters[(int)(value % 62)]);
            value /= 62;
        }

        return base62.ToString();
    }
}