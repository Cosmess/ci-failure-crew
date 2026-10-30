using CiFailureCrew.Core.Diff;

namespace CiFailureCrew.Tests;

public class DiffAnnotatorTests
{
    [Fact]
    public void Numbers_added_and_context_lines_with_their_new_line_number()
    {
        var diff = """
            diff --git a/src/Checkout.cs b/src/Checkout.cs
            --- a/src/Checkout.cs
            +++ b/src/Checkout.cs
            @@ -14,4 +14,5 @@ public sealed class Checkout
                     var subtotal = Sum(cart);
            -        var discount = discounts.For(customer);
            +        var discount = discounts.For(customer);
            +        var loyalty = 0.10m;

            -        return subtotal * (1 - discount);
            +        return subtotal * (1 - discount) * (1 - loyalty);
            """;

        var annotated = DiffAnnotator.WithLineNumbers(diff).Split('\n');

        Assert.Equal("@@ -14,4 +14,5 @@ public sealed class Checkout", annotated[3]);
        Assert.Equal("   14          var subtotal = Sum(cart);", annotated[4]);
        Assert.Equal("      -        var discount = discounts.For(customer);", annotated[5]);
        Assert.Equal("   16 +        var loyalty = 0.10m;", annotated[7]);
        Assert.Equal("   18 +        return subtotal * (1 - discount) * (1 - loyalty);", annotated[10]);
    }

    [Fact]
    public void Line_numbers_agree_with_the_lines_the_parser_accepts_for_comments()
    {
        var diff = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "test.diff"));

        var numbered = DiffAnnotator.WithLineNumbers(diff).Split('\n')
            .Select(l => l.Length > 5 && int.TryParse(l[..5], out var n) ? n : (int?)null)
            .OfType<int>()
            .ToHashSet();

        Assert.Equal(DiffParser.Parse(diff).Single().CommentableLines.Order(), numbered.Order());
    }
}
