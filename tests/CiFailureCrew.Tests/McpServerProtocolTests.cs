using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CiFailureCrew.Tests;

/// <summary>Talks to the real MCP server process over stdio, as the CLI does.</summary>
public sealed class McpServerProtocolTests : IAsyncLifetime
{
    private readonly string _root = Directory.CreateTempSubdirectory("cfc-mcp-").FullName;
    private McpClient _client = null!;

    public async Task InitializeAsync()
    {
        File.WriteAllText(Path.Combine(_root, "Program.cs"), "Console.WriteLine(\"hello\");");
        File.WriteAllText(Path.Combine(_root, ".env"), "OPENAI_API_KEY=sk-not-a-real-secret");

        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "repository",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "CiFailureCrew.McpServer.dll"), "--root", _root]
        });
        _client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        Directory.Delete(_root, recursive: true);
    }

    private static string Text(CallToolResult result) => string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));

    [Fact]
    public async Task Exposes_the_read_only_repository_tools()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Equal(["list_directory", "read_conventions", "read_file", "search_code"], tools.Select(t => t.Name).Order());
        Assert.All(tools, t => Assert.True(t.ProtocolTool.Annotations?.ReadOnlyHint));
    }

    [Fact]
    public async Task Reads_a_file_through_the_protocol()
    {
        var result = await _client.CallToolAsync("read_file", new Dictionary<string, object?> { ["path"] = "Program.cs" });

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("1| Console.WriteLine(\"hello\");", Text(result));
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("../../../../etc/passwd")]
    public async Task Returns_an_error_instead_of_protected_content(string path)
    {
        var result = await _client.CallToolAsync("read_file", new Dictionary<string, object?> { ["path"] = path });

        Assert.True(result.IsError);
        Assert.Contains("Access denied", Text(result));
        Assert.DoesNotContain("sk-not-a-real-secret", Text(result));
    }
}
