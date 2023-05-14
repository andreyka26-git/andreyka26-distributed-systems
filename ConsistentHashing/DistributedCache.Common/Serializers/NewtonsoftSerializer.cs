using Newtonsoft.Json;
using System.Text;

namespace DistributedCache.Common.Serializers
{
    public class NewtonsoftSerializer : IBinarySerializer
    {
        public byte[] Serialize<T>(T obj)
        {
            var strObj = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(strObj);
            return bytes;
        }

        public T? Deserialize<T>(string str)
            where T : class
        {
            if (typeof(T) == typeof(string))
            {
                return str as T;
            }

            return JsonConvert.DeserializeObject<T>(str);
        }
    }
}
