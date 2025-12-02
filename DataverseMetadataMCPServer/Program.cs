using DataverseMetadataMCPServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;

var builder = Host.CreateApplicationBuilder(args);

// Configure all logs to go to stderr (stdout is used for the MCP protocol messages).
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

// Add the MCP services: the transport to use (stdio) and the tools to register.
builder.Services
    .AddSingleton<IOrganizationServiceAsync>(_ =>
    {
        // Create and return the Dataverse organization service.
        var connectionString = Environment.GetEnvironmentVariable("DATAVERSE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("DATAVERSE_CONNECTION_STRING environment variable is not set.");
        }

        return new ServiceClient(connectionString);
    })
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<DataverseMetadataTools>();

await builder.Build().RunAsync();
