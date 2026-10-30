using Anthropic;
using CiFailureCrew.Core.Telemetry;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace CiFailureCrew.Providers;

/// <summary>Creates the <see cref="IChatClient"/> for the provider selected through environment variables.</summary>
public static class ChatClientFactory
{
    /// <summary>Provider and model selected by CFC_PROVIDER and CFC_MODEL, with the defaults applied.</summary>
    public static (string Provider, string Model) Selection
    {
        get
        {
            var provider = (Environment.GetEnvironmentVariable("CFC_PROVIDER") ?? "openai").ToLowerInvariant();
            var model = Environment.GetEnvironmentVariable("CFC_MODEL") is { Length: > 0 } configured
                ? configured
                : provider switch
                {
                    "ollama" => "qwen3:8b",
                    "anthropic" => "claude-sonnet-5-5",
                    _ => "gpt-5-mini"
                };

            return (provider, model);
        }
    }

    public static IChatClient Create()
    {
        var (provider, model) = Selection;

        IChatClient client = provider switch
        {
            "ollama" => new OllamaApiClient(
                new Uri(Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434"),
                model),

            "openai" => new OpenAIClient(RequireEnv("OPENAI_API_KEY"))
                .GetChatClient(model)
                .AsIChatClient(),

            "anthropic" => new AnthropicClient { ApiKey = RequireEnv("ANTHROPIC_API_KEY") }
                .AsIChatClient(model),

            _ => throw new InvalidOperationException($"Unknown provider '{provider}'. Use openai, ollama or anthropic.")
        };

        // One gen_ai "chat" span per model call, nested under the agent span. Prompts and code are not recorded.
        return client.AsBuilder()
            .UseOpenTelemetry(sourceName: CrewTelemetry.SourceName)
            .Build();
    }

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"The environment variable {name} is not set.");
}
