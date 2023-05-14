namespace DistributedCache.Common.Serializers
{
    public interface IBinarySerializer
    {
        byte[] Serialize<T>(T obj);
        T? Deserialize<T>(string str) where T : class;
    }
}
