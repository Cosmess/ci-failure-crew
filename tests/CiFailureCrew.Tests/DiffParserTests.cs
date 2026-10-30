using CiFailureCrew.Core.Diff;

namespace CiFailureCrew.Tests;

public class DiffParserTests
{
    private const string GitDiff = """
        diff --git a/src/A.cs b/src/A.cs
        index 1111111..2222222 100644
        --- a/src/A.cs
        +++ b/src/A.cs
        @@ -10,4 +10,5 @@ public class A
             var x = 1;
        -    var y = 2;
        +    var y = 3;
        +    var z = 4;
             return x;
         }
        diff --git a/src/Removed.cs b/src/Removed.cs
        deleted file mode 100644
        --- a/src/Removed.cs
        +++ /dev/null
        @@ -1,2 +0,0 @@
        -class Removed {}
        -// bye
        diff --git a/docs/New.md b/docs/New.md
        new file mode 100644
        --- /dev/null
        +++ b/docs/New.md
        @@ -0,0 +1,2 @@
        +# Title
        +--- not a header, just content
        """;

    [Fact]
    public void Splits_the_diff_into_files_with_their_paths()
    {
        var files = DiffParser.Parse(GitDiff);

        Assert.Equal(["src/A.cs", "src/Removed.cs", "docs/New.md"], files.Select(f => f.Path));
        Assert.All(files, f => Assert.StartsWith("diff --git", f.Text));
    }

    [Fact]
    public void Tracks_new_file_line_numbers_that_can_receive_comments()
    {
        var file = DiffParser.Parse(GitDiff)[0];

        Assert.Equal([10, 11, 12, 13, 14], file.CommentableLines.Order());
    }

    [Fact]
    public void Deleted_files_have_no_commentable_lines()
    {
        Assert.Empty(DiffParser.Parse(GitDiff)[1].CommentableLines);
    }

    [Fact]
    public void Content_that_looks_like_a_header_inside_a_hunk_is_not_a_new_file()
    {
        var file = DiffParser.Parse(GitDiff)[2];

        Assert.Equal([1, 2], file.CommentableLines.Order());
        Assert.Contains("--- not a header", file.Text);
    }

    [Fact]
    public void Parses_diffs_without_git_headers()
    {
        const string diff = """
            --- a/One.cs
            +++ b/One.cs
            @@ -1 +1 @@
            -a
            +b
            --- a/Two.cs
            +++ b/Two.cs
            @@ -5 +5,2 @@
            -c
            +d
            +e
            """;

        var files = DiffParser.Parse(diff);

        Assert.Equal(["One.cs", "Two.cs"], files.Select(f => f.Path));
        Assert.Equal([5, 6], files[1].CommentableLines.Order());
    }

    [Fact]
    public void A_git_header_ends_a_hunk_with_wrong_line_counts()
    {
        const string diff = """
            diff --git a/One.cs b/One.cs
            --- a/One.cs
            +++ b/One.cs
            @@ -1,5 +1,5 @@
            +only one line
            diff --git a/Two.cs b/Two.cs
            --- a/Two.cs
            +++ b/Two.cs
            @@ -0,0 +1 @@
            +b
            """;

        Assert.Equal(["One.cs", "Two.cs"], DiffParser.Parse(diff).Select(f => f.Path));
    }
}
