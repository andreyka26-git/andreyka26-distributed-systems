using System;
using System.Threading.Tasks;
using UrlShortener.UniqueNumberGeneration;

namespace UrlShortener.ShortUrlGeneration
{
    public class ShortUrlGeneratorWithUniqueNumber : IShortUrlGeneratorStrategy
    {
        private readonly IUniqueIdClient _uniqueIdClient;

        // Parameters for scrambling
        private const long Modulus = 3521614606208; // 62^7 ~ max for 7 chars
        private const long Multiplier = 119394047;  // Must be coprime with Modulus
        private const long Increment = 123456789;   // Optional, can add extra scrambling

        public ShortUrlGeneratorWithUniqueNumber(IUniqueIdClient uniqueIdClient)
        {
            _uniqueIdClient = uniqueIdClient;
        }

        public async Task<string> CreateShortUrlAsync(string originalUrl)
        {
            var id = await _uniqueIdClient.GetUniqueIdAsync();

            var scrambledId = ScrambleId(id);

            var shortUrl = Base62Utils.ToBase62(scrambledId).PadLeft(7, '0');
            return shortUrl;
        }

        /// <summary>
        /// Used to generate a pseudo-random but deterministic mapping from id to scrambledId.
        /// Otherwise in the beginning short url from number "1" would have been "1"
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private static long ScrambleId(long id)
        {
            long scrambled = (id * Multiplier + Increment) % Modulus;

            if (scrambled < 0)
                scrambled += Modulus;

            return scrambled;
        }
    }
}
