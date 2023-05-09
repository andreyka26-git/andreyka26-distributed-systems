using System.Data.HashFunction.Jenkins;

namespace DistributedCache.Common
{
    // https://en.wikipedia.org/wiki/Jenkins_hash_function
    // We are using separate hash function algorithm (not GetHashCode()) because
    // GetHashCode is not that stable, and we would like to not be coupled to C#
    public class JenkinsHashService : IHashService
    {
        private readonly IJenkinsOneAtATime _jenkinsOneAtATime = JenkinsOneAtATimeFactory.Instance.Create();

        private readonly IBinarySerializer _serializer;

        public JenkinsHashService(IBinarySerializer serializer)
        {
            _serializer = serializer;
        }

        public uint GetHash<T>(T key)
        {
            var bytes = _serializer.Serialize(key);

            // according to https://en.wikipedia.org/wiki/Jenkins_hash_function
            // hash value is 32 bit integer (uint32_t)
            var hash = _jenkinsOneAtATime.ComputeHash(bytes);
            var hashBytes = hash.Hash;

            if (hashBytes.Length != 4)
                throw new Exception($"Cannot convert from bytes to uint32 because byte length is not equal to 4. Its {bytes.Length}");

            var intHash = BitConverter.ToUInt32(hashBytes, 0);

            return intHash;
        }
    }
}
