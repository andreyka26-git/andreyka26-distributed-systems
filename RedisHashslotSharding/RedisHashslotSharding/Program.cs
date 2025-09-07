using RedisHashslotSharding;
using RedisHashslotSharding.Common;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<HashService>();
builder.Services.AddSingleton<NodeManager>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<SerialExecutionMiddleware>();


app.MapGet("/snapshot", (NodeManager nodeManager) =>
    {
        var snapshot = nodeManager.GetSnapshotAsync();
        return snapshot;
    });

app.Run();