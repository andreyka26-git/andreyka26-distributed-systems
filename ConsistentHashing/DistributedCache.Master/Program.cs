using DistributedCache.Common.Clients;
using DistributedCache.Common.NodeManagement;
using DistributedCache.Master;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMasterService, MasterService>();
builder.Services.AddSingleton<IChildNodeClient, ChildNodeClient>();
builder.Services.AddSingleton<INodeManager, NodeManager>();
//builder.Services.AddSingleton<IPhysicalNodeProvider, PhysicalNodeProvider>();
builder.Services.AddSingleton<ILoadBalancerNodeClient, LoadBalancerNodeClient>();
builder.Services.Configure<LoadBalancerOptions>(builder.Configuration.GetSection(nameof(LoadBalancerOptions)));

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
