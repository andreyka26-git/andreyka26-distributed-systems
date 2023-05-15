using DistributedCache.ChildNode;
using DistributedCache.Common;
using DistributedCache.Common.Clients;
using DistributedCache.Common.Serializers;
using Microsoft.Extensions.DependencyInjection.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IChildNodeService, ChildNodeService>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICustomHttpClient, CustomHttpClient>();

builder.Services.AddSingleton<IBinarySerializer, NewtonsoftSerializer>();
builder.Services.AddSingleton<IRebalancingQueue, RebalancingQueue>();

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
