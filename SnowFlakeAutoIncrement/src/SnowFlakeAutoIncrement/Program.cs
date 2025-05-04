using SnowFlakeAutoIncrement;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<AutoIncrementService>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect("redis:6379"));

var app = builder.Build();

app.Urls.Add("http://+:80");

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

const int NodeIdBits = 10;
const int SequenceBits = 12;

app.MapGet("identifier", async (
        AutoIncrementService service,
        IConfiguration config,
        ILogger<Program> logger,
        IConnectionMultiplexer redis) =>
    {
        var now = DateTime.UtcNow;
        
        var milliseconds = AutoIncrementService.GetMilliseconds(now);
        var sequenceNumber = service.GetSequenceNumber(milliseconds);
        var machineId = config.GetValue<uint>("MachineId");
        
        const int millisecondsShift = NodeIdBits + SequenceBits;
        const int machineIdShift = SequenceBits;

        var identifier = (milliseconds << millisecondsShift) |
                         (machineId << machineIdShift) |
                         sequenceNumber;

        logger.LogInformation($"{milliseconds}-{machineId}-{sequenceNumber} " +
                              $"Identifier: {identifier}");

        // TODO to be removed
        // Task.Run(async () =>
        // {
        //     var db = redis.GetDatabase();
        //     var key = $"{milliseconds}-{machineId}-{sequenceNumber}";
        //     var newCount = await db.StringIncrementAsync(key);
        //     logger.LogInformation($"Identifier {identifier} has been written {newCount} times.");
        // });

        return identifier;
    })
    .WithName("GetNewIdentifier")
    .WithOpenApi();

// app.MapGet("identifiers/used-more-than-once", async (
//         IConnectionMultiplexer redis,
//         ILogger<Program> logger) =>
//     {
//         var db = redis.GetDatabase();
//         var server = redis.GetServer("redis", 6379); // Assumes single node, no password
//
//         var result = new List<object>();
//
//         foreach (var key in server.Keys())
//         {
//             var value = await db.StringGetAsync(key);
//             if (value.HasValue && int.TryParse(value, out var count) && count > 1)
//             {
//                 result.Add(new { Identifier = key.ToString(), Count = count });
//             }
//         }
//
//         logger.LogInformation($"Found {result.Count} identifiers with count > 1.");
//
//         return Results.Ok(result);
//     })
//     .WithName("GetIdentifiersUsedMoreThanOnce")
//     .WithOpenApi();

app.Run();