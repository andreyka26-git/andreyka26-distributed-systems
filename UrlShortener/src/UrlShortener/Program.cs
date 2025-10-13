using UrlShortener;
using UrlShortener.ShortUrlGeneration;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient();
builder.Services.AddTransient<IUniqueIdClient, UniqueIdClient>();
builder.Services.AddTransient<IShortUrlGeneratorFactory, ShortUrlGeneratorFactory>();
builder.Services.AddSingleton<UrlShortenerService>();

// Add Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();

app.Urls.Add("http://+:80");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

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
