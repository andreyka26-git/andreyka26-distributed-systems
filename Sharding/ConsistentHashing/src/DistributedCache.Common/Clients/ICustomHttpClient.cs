namespace DistributedCache.Common.Clients
{
    public interface ICustomHttpClient
    {
        Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken);

        Task PostAsync<T>(Uri url, T req, CancellationToken cancellationToken);

        Task<TRes?> PostAsync<TReq, TRes>(Uri url, TReq req, CancellationToken cancellationToken);
        Task DeleteAsync(Uri url, CancellationToken cancellationToken);
        Task DeleteAsync<T>(Uri url, T req, CancellationToken cancellationToken);
    }
}
