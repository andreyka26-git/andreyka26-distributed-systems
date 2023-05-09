namespace DistributedCache.Common
{
    public interface IHashService
    {
        uint GetHash<T>(T key);
    }
}
