namespace DistributedCache.Common.Serializers
{
    public interface IBinarySerializer
    {
        string SerializeToJson<T>(T obj);
        byte[] SerializeToBinary<T>(T obj);
        T? Deserialize<T>(string str);
    }
}
