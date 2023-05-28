namespace DistributedCache.Common.Concurrency
{
    public interface IReadWriteLockService : IDisposable
    {
        T Read<T>(Func<T> func);
        void Write(Action func);
        T Write<T>(Func<T> func);
    }
}
