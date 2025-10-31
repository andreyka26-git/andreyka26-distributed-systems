namespace UrlShortener.UniqueNumberGeneration;

public class SnowflakeUniqueIdClient : IUniqueIdClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public SnowflakeUniqueIdClient(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<long> GetUniqueIdAsync()
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync($"{_config.GetValue<string>("SnowflakeBaseUrl")}/identifier");

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error while calling UniequeId service, error: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync();
        return long.Parse(content);
    }
}