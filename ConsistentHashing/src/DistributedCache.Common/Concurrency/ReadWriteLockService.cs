namespace DistributedCache.Common.Concurrency
{
    public class ReadWriteLockService : IReadWriteLockService
    {
        private TimeSpan _lockWaitTimeout = TimeSpan.FromMinutes(1);
        private readonly ReaderWriterLockSlim _readWriteLock = new ReaderWriterLockSlim();

        public T Read<T>(Func<T> func)
        {
            if (!_readWriteLock.TryEnterReadLock(_lockWaitTimeout))
            {
                throw new Exception($"{nameof(Read)} cannot enter read lock");
            }

            try
            {
                return func();
            }
            finally
            {
                _readWriteLock.ExitReadLock();
            }
        }

        public void Write(Action func)
        {
            if (!_readWriteLock.TryEnterWriteLock(_lockWaitTimeout))
            {
                throw new Exception($"{nameof(Write)} cannot enter write lock");
            }

            try
            {
                func();
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        public T Write<T>(Func<T> func)
        {
            if (!_readWriteLock.TryEnterWriteLock(_lockWaitTimeout))
            {
                throw new Exception($"{nameof(Write)} cannot enter write lock");
            }

            try
            {
                return func();
            }
            finally
            {
                _readWriteLock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _readWriteLock.Dispose();
        }
    }
}
