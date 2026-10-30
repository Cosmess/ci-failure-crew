using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Telemetry;
using Microsoft.Extensions.AI;

namespace CiFailureCrew.Core.Orchestration;

/// <summary>Thread-safe accumulator of the tokens spent by every agent call.</summary>
internal sealed class UsageTracker(PricingConfig? pricing)
{
    private long _input;
    private long _output;

    public void Add(UsageDetails? usage, string agentName)
    {
        var input = usage?.InputTokenCount ?? 0;
        var output = usage?.OutputTokenCount ?? 0;
        Interlocked.Add(ref _input, input);
        Interlocked.Add(ref _output, output);

        var agentTag = new KeyValuePair<string, object?>("gen_ai.agent.name", agentName);
        CrewTelemetry.InputTokens.Add(input, agentTag);
        CrewTelemetry.OutputTokens.Add(output, agentTag);
    }

    public UsageSummary ToSummary()
    {
        decimal? cost = pricing is null
            ? null
            : (_input * pricing.InputPerMillion + _output * pricing.OutputPerMillion) / 1_000_000m;

        return new UsageSummary(_input, _output, cost);
    }
}
