namespace UrlShortener;

public interface IUniqueIdClient
{
    Task<long> GetUniqueIdAsync();
}