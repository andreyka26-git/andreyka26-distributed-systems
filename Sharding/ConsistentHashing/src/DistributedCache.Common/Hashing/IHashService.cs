namespace DistributedCache.Common.Hashing
{
    public interface IHashService
    {
        uint MaxHashValue { get; }
        uint GetHash<T>(T key);
    }
}
