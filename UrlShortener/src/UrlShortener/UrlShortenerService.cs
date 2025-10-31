using System.Text;
using Dapper;
using Npgsql;
using UrlShortener.ShortUrlGeneration;
using UrlShortener.Database;

namespace UrlShortener;

public class UrlShortenerService
{
    private readonly IConfiguration _config;
    private readonly DatabaseSetup _databaseSetup;
    private readonly IShortUrlGeneratorFactory _generatorFactory;
    private readonly ILogger<UrlShortenerService> _logger;

    public UrlShortenerService(
        IConfiguration config,
        DatabaseSetup databaseSetup,
        IShortUrlGeneratorFactory generatorFactory,
        ILogger<UrlShortenerService> logger)
    {
        _config = config;
        _databaseSetup = databaseSetup;
        _generatorFactory = generatorFactory;
        _logger = logger;
    }

    public async Task<string> CreateShortUrlAsync(string originalUrl)
    {
        var strategy = _generatorFactory.CreateStrategy();
        var shortUrl = await strategy.CreateShortUrlAsync(originalUrl);
        await StoreUrlMappingAsync(shortUrl, originalUrl);
        return shortUrl;
    }

    public async Task<string?> GetOriginalUrlAsync(string shortUrl)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured.");
        }

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT original_url 
                FROM url_mappings 
                WHERE short_url = @shortUrl";

            var originalUrl = await connection.QuerySingleOrDefaultAsync<string>(selectSql, new { shortUrl });
            
            if (originalUrl != null)
            {
                _logger.LogDebug("Retrieved URL mapping: {ShortUrl} -> {OriginalUrl}", shortUrl, originalUrl);
            }
            
            return originalUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve original URL for short URL: {ShortUrl}", shortUrl);
            throw;
        }
    }

    private async Task StoreUrlMappingAsync(string shortUrl, string originalUrl)
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO url_mappings (short_url, original_url, created_at) 
                VALUES (@shortUrl, @originalUrl, NOW())
                ON CONFLICT (short_url) DO UPDATE SET 
                    original_url = EXCLUDED.original_url,
                    updated_at = NOW()";

            await connection.ExecuteAsync(insertSql, new { shortUrl, originalUrl });
            
            _logger.LogDebug("Stored URL mapping: {ShortUrl} -> {OriginalUrl}", shortUrl, originalUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store URL mapping: {ShortUrl} -> {OriginalUrl}", shortUrl, originalUrl);
            throw;
        }
    }
}