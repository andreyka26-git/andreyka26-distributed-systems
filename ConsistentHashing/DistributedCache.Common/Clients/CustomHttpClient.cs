using DistributedCache.Common.Serializers;

namespace DistributedCache.Common.Clients
{
    public class CustomHttpClient : ICustomHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly IBinarySerializer _serializer;

        public CustomHttpClient(IHttpClientFactory factory, IBinarySerializer serializer)
        {
            _httpClient = factory.CreateClient();
            _serializer = serializer;
        }

        public async Task<T> GetAsync<T>(Uri url, CancellationToken cancellationToken)
            where T : class, struct
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while sending request to {url}, error: {content}");
                }

                var deserializedResponse = _serializer.Deserialize<T>(content);
                return deserializedResponse;
            }
        }

        public async Task PostAsync<T>(Uri url, T req, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while sending request to {url}");
                }
            }
        }

        public async Task<TRes?> PostAsync<TReq, TRes>(Uri url, TReq req, CancellationToken cancellationToken)
            where TRes : class
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while sending request to {url}, error: {content}");
                }

                var deserializedResponse = _serializer.Deserialize<TRes>(content);
                return deserializedResponse;
            }
        }
    }
}
