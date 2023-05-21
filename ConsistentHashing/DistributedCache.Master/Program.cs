using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Concurrency;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;
using DistributedCache.Master;
using System.Xml.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMasterService, MasterService>();
builder.Services.AddSingleton<IChildNodeClient, ChildNodeClient>();
builder.Services.AddSingleton<ILoadBalancerNodeClient, LoadBalancerNodeClient>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICustomHttpClient, CustomHttpClient>();

builder.Services.AddSingleton<IChildNodeManager, ChildNodeManager>();
builder.Services.AddSingleton<IPhysicalNodeProvider, PhysicalNodeProvider>();
builder.Services.AddSingleton<IBinarySerializer, NewtonsoftSerializer>();
builder.Services.AddSingleton<IHashingRing, HashingRing>();
builder.Services.AddSingleton<IHashService, JenkinsHashService>();
builder.Services.AddSingleton<IAsyncSerializableLockService, AsyncSerializableLockService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

InitializeMasterAsync().GetAwaiter().GetResult();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

async Task InitializeMasterAsync()
{
    using (var scope = app.Services.CreateScope())
    {
        var masterService = scope.ServiceProvider.GetRequiredService<IMasterService>();

        // order matters
        await masterService.CreateLoadBalancerAsync(7005, CancellationToken.None);
        await masterService.CreateLoadBalancerAsync(7006, CancellationToken.None);

        await masterService.CreateNewChildNodeAsync(7007, CancellationToken.None);
        await masterService.CreateNewChildNodeAsync(7008, CancellationToken.None);
    }
}