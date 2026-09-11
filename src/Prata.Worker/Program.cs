using Prata.Infrastructure;
using Prata.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPrataInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
