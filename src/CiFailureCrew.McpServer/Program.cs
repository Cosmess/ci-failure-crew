using CiFailureCrew.McpServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP server over stdio. Usage: CiFailureCrew.McpServer --root <repository directory>
var rootIndex = Array.IndexOf(args, "--root");
var root = rootIndex >= 0 && rootIndex + 1 < args.Length ? args[rootIndex + 1] : Directory.GetCurrentDirectory();

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

// stdout carries the MCP protocol: logs must go to stderr.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(new RepositoryReader(root));
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<RepositoryTools>();

await builder.Build().RunAsync();
