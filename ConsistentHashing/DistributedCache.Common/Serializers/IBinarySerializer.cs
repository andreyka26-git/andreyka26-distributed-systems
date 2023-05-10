namespace DistributedCache.Common.Serializers
{
    public interface IBinarySerializer
    {
        byte[] Serialize<T>(T obj);
    }
}
