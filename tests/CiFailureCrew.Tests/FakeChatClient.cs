using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace CiFailureCrew.Tests;

/// <summary>Deterministic <see cref="IChatClient"/> that answers based on which agent is calling.</summary>
internal sealed class FakeChatClient(Func<string, CancellationToken, Task<string>> respond) : IChatClient
{
    public FakeChatClient(Func<string, string> respond)
        : this((prompt, _) => Task.FromResult(respond(prompt)))
    {
    }

    public ConcurrentQueue<string> Prompts { get; } = [];

    public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var prompt = string.Join("\n", new[] { options?.Instructions }.Concat(messages.Select(m => m.Text)));
        Prompts.Enqueue(prompt);

        var text = await respond(prompt, cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            Usage = new UsageDetails { InputTokenCount = 1000, OutputTokenCount = 100 }
        };
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
