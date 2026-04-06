using ACSdemo.Application.Configuration;
using ACSdemo.Domain.Configuration;
using ACSdemo.Infrastructure.Configuration;
using ACSdemo.Web.Configuration;
using Azure.Identity;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

var builder = WebApplication.CreateBuilder(args);

var appConfigEndpoint = builder.Configuration["Azure:AppConfig:Endpoint"];
if (!string.IsNullOrEmpty(appConfigEndpoint))
{
    builder.Configuration.AddAzureAppConfiguration(options =>
        options.Connect(new Uri(appConfigEndpoint), new DefaultAzureCredential()));
}

builder.Services.AddDomainServices();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddWebServices();

builder.Services.AddControllers().AddDapr();
builder.Services.AddDaprClient();

var app = builder.Build();

app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();
app.MapGraphQL();

app.Run();

