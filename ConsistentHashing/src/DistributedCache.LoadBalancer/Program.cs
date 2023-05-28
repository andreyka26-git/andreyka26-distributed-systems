using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Hashing;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Common.Serializers;
using DistributedCache.LoadBalancer;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddNewtonsoftJson();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<ILoadBalancerService, LoadBalancerService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICustomHttpClient, CustomHttpClient>();
builder.Services.AddSingleton<IChildNodeClient, ChildNodeClient>();

builder.Services.AddSingleton<IHashingRing, HashingRing>();
builder.Services.AddSingleton<IChildNodeManager, ChildNodeManager>();
builder.Services.AddSingleton<IHashService, JenkinsHashService>();
builder.Services.AddSingleton<IBinarySerializer, NewtonsoftSerializer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
