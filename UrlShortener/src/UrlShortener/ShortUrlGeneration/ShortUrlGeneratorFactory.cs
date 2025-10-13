using Microsoft.Extensions.Configuration;

namespace UrlShortener.ShortUrlGeneration;

public interface IShortUrlGeneratorFactory
{
    IShortUrlGeneratorStrategy CreateStrategy();
}

public class ShortUrlGeneratorFactory : IShortUrlGeneratorFactory
{
    private readonly IConfiguration _configuration;
    private readonly IUniqueIdClient _uniqueIdClient;

    public ShortUrlGeneratorFactory(IConfiguration configuration, IUniqueIdClient uniqueIdClient)
    {
        _configuration = configuration;
        _uniqueIdClient = uniqueIdClient;
    }

    public IShortUrlGeneratorStrategy CreateStrategy()
    {
        var strategyName = _configuration["ShortUrlGenerator:Strategy"] ?? "UniqueNumber";
        
        return strategyName.ToLowerInvariant() switch
        {
            "hashing" => new ShortUrlGeneratorWithHashing(),
            "uniquenumber" => new ShortUrlGeneratorWithUniqueNumber(_uniqueIdClient),
            "guid" => new ShortUrlGeneratorWithGuid(),
            "random" => new ShortUrlGeneratorWithRandom(),
            "base62" => new ShortUrlGeneratorWithBase62(),
            _ => new ShortUrlGeneratorWithUniqueNumber(_uniqueIdClient) // Default strategy
        };
    }
}