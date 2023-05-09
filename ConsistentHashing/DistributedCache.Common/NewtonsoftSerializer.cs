using Newtonsoft.Json;
using System.Text;

namespace DistributedCache.Common
{
    public class NewtonsoftSerializer : IBinarySerializer
    {
        public byte[] Serialize<T>(T obj)
        {
            var strObj = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(strObj);
            return bytes;
        }
    }
}
