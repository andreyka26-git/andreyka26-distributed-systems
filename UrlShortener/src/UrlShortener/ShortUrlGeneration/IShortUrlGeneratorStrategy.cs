namespace UrlShortener.ShortUrlGeneration;

public interface IShortUrlGeneratorStrategy
{
    Task<string> CreateShortUrlAsync(string originalUrl);
}