using Dapper;
using Npgsql;

namespace UrlShortener.Database;

public class DatabaseSetup
{
    private readonly IConfiguration _config;
    private readonly ILogger<DatabaseSetup> _logger;

    public DatabaseSetup(IConfiguration config, ILogger<DatabaseSetup> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task InitializeDatabaseAsync()
    {
        var connectionString = _config.GetConnectionString("DefaultConnection");
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Database connection string 'DefaultConnection' is not configured.");
        }

        await EnsureDatabaseExistsAsync(connectionString);
        await EnsureTablesExistAsync(connectionString);
    }

    private async Task EnsureDatabaseExistsAsync(string connectionString)
    {
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var databaseName = builder.Database;
            
            // Connect to 'postgres' database to check if target database exists
            builder.Database = "postgres";
            var adminConnectionString = builder.ToString();

            using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();

            // Check if database exists
            const string checkDbSql = "SELECT 1 FROM pg_database WHERE datname = @databaseName";
            var dbExists = await connection.QuerySingleOrDefaultAsync<int?>(checkDbSql, new { databaseName });

            if (dbExists == null)
            {
                _logger.LogInformation("Creating database: {DatabaseName}", databaseName);
                
                // Create database
                var createDbSql = $"CREATE DATABASE \"{databaseName}\"";
                await connection.ExecuteAsync(createDbSql);
                
                _logger.LogInformation("Database {DatabaseName} created successfully", databaseName);
            }
            else
            {
                _logger.LogInformation("Database {DatabaseName} already exists", databaseName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure database exists. Make sure PostgreSQL is running and accessible.");
            throw;
        }
    }

    private async Task EnsureTablesExistAsync(string connectionString)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Check if unique_ids table exists
            const string checkUniqueIdsTableSql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = 'unique_ids'
                );";

            var uniqueIdsTableExists = await connection.QuerySingleAsync<bool>(checkUniqueIdsTableSql);

            if (!uniqueIdsTableExists)
            {
                _logger.LogInformation("Creating unique_ids table");
                
                const string createUniqueIdsTableSql = @"
                    CREATE TABLE unique_ids (
                        id BIGSERIAL PRIMARY KEY,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    
                    CREATE INDEX idx_unique_ids_created_at ON unique_ids(created_at);";

                await connection.ExecuteAsync(createUniqueIdsTableSql);
                
                _logger.LogInformation("Table unique_ids created successfully");
            }
            else
            {
                _logger.LogInformation("Table unique_ids already exists");
            }

            // Check if url_mappings table exists
            const string checkUrlMappingsTableSql = @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables 
                    WHERE table_schema = 'public' 
                    AND table_name = 'url_mappings'
                );";

            var urlMappingsTableExists = await connection.QuerySingleAsync<bool>(checkUrlMappingsTableSql);

            if (!urlMappingsTableExists)
            {
                _logger.LogInformation("Creating url_mappings table");
                
                const string createUrlMappingsTableSql = @"
                    CREATE TABLE url_mappings (
                        short_url VARCHAR(255) PRIMARY KEY,
                        original_url TEXT NOT NULL,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        updated_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    );
                    
                    CREATE INDEX idx_url_mappings_created_at ON url_mappings(created_at);
                    CREATE INDEX idx_url_mappings_original_url ON url_mappings(original_url);";

                await connection.ExecuteAsync(createUrlMappingsTableSql);
                
                _logger.LogInformation("Table url_mappings created successfully");
            }
            else
            {
                _logger.LogInformation("Table url_mappings already exists");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure tables exist");
            throw;
        }
    }
}