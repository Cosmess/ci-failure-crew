using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace CiFailureCrew.Cli;

/// <summary>
/// Starts the read-only repository MCP server and exposes its tools to the agents.
/// If it fails to start, the explanation continues without it.
/// </summary>
internal sealed class McpToolHost : IAsyncDisposable
{
    private const string RepositoryServerDll = "CiFailureCrew.McpServer.dll";

    // The only variables a server inherits: API keys and tokens of this process never reach it.
    private static readonly string[] SafeEnvironmentVariables = ["PATH", "HOME", "USERPROFILE", "TMPDIR", "TEMP", "DOTNET_ROOT", "SystemRoot"];

    private readonly List<McpClient> _clients = [];

    public List<AITool> Tools { get; } = [];

    public static async Task<McpToolHost> StartAsync(string? repositoryPath, CancellationToken cancellationToken)
    {
        var host = new McpToolHost();
        if (repositoryPath is not null)
        {
            await host.ConnectAsync(
                "repository", "dotnet", [Path.Combine(AppContext.BaseDirectory, RepositoryServerDll), "--root", Path.GetFullPath(repositoryPath)], cancellationToken);
        }

        return host;
    }

    private async Task ConnectAsync(string name, string command, IList<string> arguments, CancellationToken cancellationToken)
    {
        var environment = SafeEnvironmentVariables
            .Select(variable => (variable, value: Environment.GetEnvironmentVariable(variable)))
            .Where(v => v.value is not null)
            .ToDictionary(v => v.variable, v => v.value);

        try
        {
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = name,
                Command = command,
                Arguments = arguments,
                InheritEnvironmentVariables = false,
                EnvironmentVariables = environment
            });

            var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            _clients.Add(client);

            var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);
            Tools.AddRange(tools);
            Console.Error.WriteLine($"MCP: server '{name}' connected with {tools.Count} tool(s): {string.Join(", ", tools.Select(t => t.Name))}");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine($"⚠ MCP: could not start server '{name}': {ex.Message}. The explanation continues without it.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients)
        {
            await client.DisposeAsync();
        }
    }
}
