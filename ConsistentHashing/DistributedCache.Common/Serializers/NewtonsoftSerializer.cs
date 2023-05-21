using Newtonsoft.Json;
using System.Text;

namespace DistributedCache.Common.Serializers
{
    public class NewtonsoftSerializer : IBinarySerializer
    {
        public string SerializeToJson<T>(T obj)
        {
            var strObj = JsonConvert.SerializeObject(obj);

            return strObj;
        }

        public byte[] SerializeToBinary<T>(T obj)
        {
            var strObj = SerializeToJson(obj);
            var bytes = Encoding.UTF8.GetBytes(strObj);
            return bytes;
        }

        public T? Deserialize<T>(string str)
        {
            if (typeof(T) == typeof(string))
            {
                // just a hack with open generics to return string without generic constraints
                return (dynamic)str;
            }

            return JsonConvert.DeserializeObject<T>(str);
        }
    }
}
