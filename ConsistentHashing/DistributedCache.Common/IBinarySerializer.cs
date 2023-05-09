namespace DistributedCache.Common
{
    public interface IBinarySerializer
    {
        byte[] Serialize<T>(T obj);
    }
}
