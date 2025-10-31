using UrlShortener;
using UrlShortener.ShortUrlGeneration;
using UrlShortener.Database;
using UrlShortener.UniqueNumberGeneration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();

// Register database setup
builder.Services.AddScoped<DatabaseSetup>();

// Register UniqueIdClient based on configuration
var uniqueIdStrategy = builder.Configuration.GetValue<string>("UniqueIdStrategy", "Snowflake");
switch (uniqueIdStrategy?.ToLowerInvariant())
{
    case "autoincrement":
        builder.Services.AddTransient<IUniqueIdClient, AutoIncrementUniqueIdClient>();
        break;
    case "snowflake":
    default:
        builder.Services.AddTransient<IUniqueIdClient, SnowflakeUniqueIdClient>();
        break;
}

builder.Services.AddTransient<IShortUrlGeneratorFactory, ShortUrlGeneratorFactory>();
builder.Services.AddScoped<UrlShortenerService>();

var app = builder.Build();

if (uniqueIdStrategy?.ToLowerInvariant() == "autoincrement")
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbSetup = scope.ServiceProvider.GetRequiredService<DatabaseSetup>();
            await dbSetup.InitializeDatabaseAsync();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Database initialization completed successfully");
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Database initialization failed");
            throw; // Fail fast if database setup fails
        }
    }
}

app.Urls.Add("http://+:80");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// GET health check endpoint
app.MapGet("/health", () => 
    {
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    })
    .WithName("HealthCheck")
    .WithOpenApi();

// POST http://localhost:5000/shortener/url
app.MapPost("/url", async (ShortUrlRequest request, UrlShortenerService service) =>
    {
        var shortUrl = await service.CreateShortUrlAsync(request.TargetUrl);
        return shortUrl;
    })
    .WithName("ShortUrl")
    .WithOpenApi();

// GET http://localhost:5000/shortener/pkIcYynZ
app.MapGet("/url/{shortCode}", async (string shortCode, UrlShortenerService service) =>
    {
        var originalUrl = await service.GetOriginalUrlAsync(shortCode);
        return originalUrl is not null
            ? Results.Redirect(originalUrl)
            : Results.NotFound("Short URL not found.");
    })
    .WithName("Redirect")
    .WithOpenApi();

app.Run();

