using Dapper;
using Npgsql;
using UrlShortener.Database;

namespace UrlShortener.UniqueNumberGeneration;

public class AutoIncrementUniqueIdClient : IUniqueIdClient
{
    private readonly IConfiguration _config;
    private readonly DatabaseSetup _databaseSetup;
    private readonly ILogger<AutoIncrementUniqueIdClient> _logger;

    public AutoIncrementUniqueIdClient(
        IConfiguration config, 
        DatabaseSetup databaseSetup,
        ILogger<AutoIncrementUniqueIdClient> logger)
    {
        _config = config;
        _databaseSetup = databaseSetup;
        _logger = logger;
    }

    public async Task<long> GetUniqueIdAsync()
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured.");
        }

        try
        {
            // Ensure database schema is set up
            await _databaseSetup.InitializeDatabaseAsync();

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Insert a new record and get the auto-incremented ID
            const string insertSql = @"
                INSERT INTO unique_ids (created_at) 
                VALUES (NOW()) 
                RETURNING id;";

            var uniqueId = await connection.QuerySingleAsync<long>(insertSql);
            
            _logger.LogDebug("Generated unique ID: {UniqueId}", uniqueId);
            
            return uniqueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate unique ID from database");
            throw;
        }
    }
}