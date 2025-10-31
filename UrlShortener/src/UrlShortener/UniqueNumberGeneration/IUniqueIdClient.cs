namespace UrlShortener.UniqueNumberGeneration;

public interface IUniqueIdClient
{
    Task<long> GetUniqueIdAsync();
}