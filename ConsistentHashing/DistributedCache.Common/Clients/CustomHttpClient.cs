using DistributedCache.Common.Serializers;
using System.Net.Http;
using System.Net.Mime;
using System.Text;

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
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                var reqJson = _serializer.SerializeToJson(req);
                request.Content = new StringContent(reqJson, Encoding.UTF8, MediaTypeNames.Application.Json);

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

        public async Task DeleteAsync(Uri url, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                var response = await _httpClient.SendAsync(request, cancellationToken);

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while sending request to {url}, error: {content}");
                }
            }
        }

        public async Task DeleteAsync<T>(Uri url, T req, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                var reqJson = _serializer.SerializeToJson(req);
                request.Content = new StringContent(reqJson, Encoding.UTF8, MediaTypeNames.Application.Json);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error while sending request to {url}, error: {content}");
                }
            }
        }
    }
}
