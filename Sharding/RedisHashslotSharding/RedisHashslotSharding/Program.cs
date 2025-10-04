using RedisHashslotSharding;
using RedisHashslotSharding.Domain;
using RedisHashslotSharding.Dtos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<HashService>();
builder.Services.AddSingleton<Server>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<SerialExecutionMiddleware>();


app.MapGet("/snapshot", async (Server server) =>
    {
        var snapshot = await server.GetSnapshotAsync();
        return snapshot;
    });

app.MapPost("/set/{key}/{value}", async (Server server, HashService hashService, string key, string value) =>
    {
        if (!server.Initialized)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Server not initialized" });
        }

        var slotId = hashService.ComputeSlotId(key);
        var node = server.HashSlots[slotId];
        
        if (node.IsNodeLocal() && node is LocalNode localNode)
        {
            if (localNode.LocalHashSlots.TryGetValue(slotId, out var cache))
            {
                cache.Entries[key] = value;
                return Results.Ok(new SetKeyResponse 
                { 
                    Key = key, 
                    Value = value, 
                    SlotId = slotId, 
                    NodeId = node.NodeId 
                });
            }
        }
        
        return Results.BadRequest(new ErrorResponse { Error = "Cannot set value - slot not available locally" });
    });

app.MapGet("/get/{key}", async (Server server, HashService hashService, string key) =>
    {
        if (!server.Initialized)
        {
            return Results.BadRequest(new ErrorResponse { Error = "Server not initialized" });
        }

        var slotId = hashService.ComputeSlotId(key);
        var node = server.HashSlots[slotId];
        
        if (node.IsNodeLocal() && node is LocalNode localNode)
        {
            if (localNode.LocalHashSlots.TryGetValue(slotId, out var cache))
            {
                if (cache.Entries.TryGetValue(key, out var value))
                {
                    return Results.Ok(new GetKeyResponse 
                    { 
                        Key = key, 
                        Value = value, 
                        SlotId = slotId, 
                        NodeId = node.NodeId 
                    });
                }
            }
        }
        
        return Results.NotFound(new ErrorResponse { Error = "Key not found" });
    });

app.MapPost("/initialize/{nodeId}", async (Server server, string nodeId) =>
    {
        await server.InitializeAsSingleInstance(nodeId, $"http://localhost:5000");
        return Results.Ok(new InitializationResponse 
        { 
            Message = "Server initialized", 
            NodeId = nodeId 
        });
    });

app.Run();