using CiFailureCrew.Core.Configuration;
using CiFailureCrew.Core.Models;
using CiFailureCrew.Core.Orchestration;
using Microsoft.Extensions.AI;
using OpenAI;

namespace CiFailureCrew.IntegrationTests;

/// <summary>Runs the crew against the real OpenAI API. Skipped when OPENAI_API_KEY is not set.</summary>
public class OpenAIExplanationTests
{
    private static string Sample(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", name));

    private static FailureOrchestrator Orchestrator(string language = "en")
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Skip.If(string.IsNullOrEmpty(key), "OPENAI_API_KEY is not set.");

        var model = Environment.GetEnvironmentVariable("CFC_MODEL") is { Length: > 0 } configured ? configured : "gpt-5-mini";
        var client = new OpenAIClient(key).GetChatClient(model).AsIChatClient();
        return new FailureOrchestrator(client, new CrewConfig { Language = language });
    }

    [SkippableFact]
    public async Task Finds_the_line_of_the_change_that_broke_the_tests()
    {
        var context = new FailureContext([new FailedJob("test", Sample("test.log"))], Sample("test.diff"), "Add loyalty discount", "CI");

        var result = await Orchestrator().ExplainAsync(context);

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.TestFailure, job.Category);
        Assert.Equal("src/Shop.Core/Checkout.cs", job.File);
        Assert.InRange(job.Line ?? 0, 15, 19);
        Assert.True(job.RelatedToPullRequest);
        Assert.Contains(job.FailingItems, i => i.Contains("Gold_customer_pays_ninety_percent"));
        Assert.Empty(job.Errors);
        Assert.False(string.IsNullOrWhiteSpace(result.Report.Title));
    }

    [SkippableFact]
    public async Task Explains_a_compilation_error_in_portuguese()
    {
        var log = string.Join('\n',
            "2026-10-30T12:00:00.0000000Z ##[group]Run dotnet build -c Release",
            "2026-10-30T12:00:05.0000000Z /home/runner/work/shop/shop/src/Shop.Api/OrdersController.cs(27,30): error CS1061: 'OrderService' does not contain a definition for 'GetTotal' and no accessible extension method 'GetTotal' accepting a first argument of type 'OrderService' could be found",
            "2026-10-30T12:00:05.1000000Z Build FAILED.",
            "2026-10-30T12:00:05.2000000Z ##[error]Process completed with exit code 1.");
        var diff = """
            diff --git a/src/Shop.Core/OrderService.cs b/src/Shop.Core/OrderService.cs
            --- a/src/Shop.Core/OrderService.cs
            +++ b/src/Shop.Core/OrderService.cs
            @@ -8,3 +8,3 @@ public sealed class OrderService(IOrderRepository orders)
             {
            -    public decimal GetTotal(Order order) => order.Items.Sum(i => i.Price);
            +    public decimal CalculateTotal(Order order) => order.Items.Sum(i => i.Price);
             }
            """;

        var result = await Orchestrator("pt-BR").ExplainAsync(new FailureContext([new FailedJob("build", log)], diff));

        var job = Assert.Single(result.Report.Jobs);
        Assert.Equal(FailureCategory.Compilation, job.Category);
        Assert.True(job.RelatedToPullRequest);
        Assert.Contains("GetTotal", job.Explanation);
        Assert.Contains(new[] { "método", "renome", "compila" }, word => job.Explanation.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
}
