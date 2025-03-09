// See https://aka.ms/new-console-template for more information

using RateLimiter.Api.Application;
using RateLimiter.Api.Infrastructure;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var redis = ConnectionMultiplexer.Connect("redis:6379");
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton<IRateLimiter, RedisBasedRateLimiter>();

// builder.Services.AddSingleton<IRateLimiter, InMemoryRateLimiter>();
builder.Services.AddSingleton<ProductionService>();

var app = builder.Build();

app.Urls.Add("http://+:80");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();