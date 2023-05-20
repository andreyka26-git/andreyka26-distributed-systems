namespace DistributedCache.Common.Concurrency
{
    public interface IAsyncSerializableLockService
    {
        Task ExecuteSeriallyAsync(Func<Task> func, CancellationToken cancellationToken = default);
        Task<T> ExecuteSeriallyAsync<T>(Func<Task<T>> func, CancellationToken cancellationToken = default);
    }
}
