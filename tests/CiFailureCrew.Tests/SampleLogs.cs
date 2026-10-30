namespace CiFailureCrew.Tests;

/// <summary>Job logs in the format GitHub returns them: a timestamp on every line and ##[group] step headers.</summary>
internal static class SampleLogs
{
    public static string WithTimestamps(params string[] lines) =>
        string.Join("\n", lines.Select((line, i) => $"2026-10-30T12:00:{i % 60:00}.{i:0000000}Z {line}"));

    public static string DotnetTestFailure() => WithTimestamps(
        "##[group]Run actions/checkout@v4",
        "Syncing repository: acme/shop",
        "##[endgroup]",
        "##[group]Run dotnet build -c Release",
        "  Shop.Core -> /home/runner/work/shop/shop/src/Shop.Core/bin/Release/net10.0/Shop.Core.dll",
        "Build succeeded.",
        "##[endgroup]",
        "##[group]Run dotnet test -c Release --no-build",
        "Starting test execution, please wait...",
        "A total of 1 test files matched the specified pattern.",
        "[xUnit.net 00:00:00.41]     Shop.Tests.DiscountTests.Applies_ten_percent_for_gold_customers [FAIL]",
        "  Failed Shop.Tests.DiscountTests.Applies_ten_percent_for_gold_customers [12 ms]",
        "  Error Message:",
        "   Assert.Equal() Failure: Values differ",
        "Expected: 90",
        "Actual:   99",
        "  Stack Trace:",
        "     at Shop.Tests.DiscountTests.Applies_ten_percent_for_gold_customers() in /home/runner/work/shop/shop/tests/Shop.Tests/DiscountTests.cs:line 14",
        "",
        "Failed!  - Failed:     1, Passed:    41, Skipped:     0, Total:    42, Duration: 310 ms - Shop.Tests.dll (net10.0)",
        "##[error]Process completed with exit code 1.",
        "Post job cleanup.");

    public static string CompilationError() => WithTimestamps(
        "##[group]Run dotnet build -c Release",
        "  Determining projects to restore...",
        "\u001b[31m/home/runner/work/shop/shop/src/Shop.Api/OrdersController.cs(27,30): error CS1061: 'OrderService' does not contain a definition for 'GetTotal' and no accessible extension method 'GetTotal' accepting a first argument of type 'OrderService' could be found [/home/runner/work/shop/shop/src/Shop.Api/Shop.Api.csproj]\u001b[0m",
        "",
        "Build FAILED.",
        "    0 Warning(s)",
        "    1 Error(s)",
        "##[error]Process completed with exit code 1.");
}
