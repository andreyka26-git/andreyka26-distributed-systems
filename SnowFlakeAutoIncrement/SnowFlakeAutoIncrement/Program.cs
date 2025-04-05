using SnowFlakeAutoIncrement;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<AutoIncrementService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

const int NodeIdBits = 10;
const int SequenceBits = 12;

app.MapPost("identifier", (
        AutoIncrementService service,
        IConfiguration config,
        ILogger<Program> logger) =>
    {
        var now = DateTime.UtcNow;
        
        var milliseconds = AutoIncrementService.GetMilliseconds(now);
        var sequenceNumber = service.GetSequenceNumber(milliseconds);
        var machineId = config.GetValue<uint>("MachineId");
        
        var millisecondsShift = NodeIdBits + SequenceBits;
        var machineIdShift = SequenceBits;
        
        var identifier = (milliseconds << millisecondsShift) | 
                         (machineId << machineIdShift) |
                         sequenceNumber;
        
        logger.LogInformation($"Milliseconds: {milliseconds}, " +
                              $"MachineId: {machineId}, " +
                              $"SequenceNumber: {sequenceNumber}");
        
        return identifier;
    })
    .WithName("GetNewIdentifier")
    .WithOpenApi();

app.Run();