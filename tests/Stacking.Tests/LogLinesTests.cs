using System;
using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The log file's queue. A recipe fill or a station move once waited on a file write per line; lines are now only
/// queued where they happen, so the writer must be woken for the first line of each batch (or lines sit unwritten)
/// and must get every line, in order, exactly once.
/// </summary>
public class LogLinesTests
{
    private static readonly DateTime At = new(2026, 10, 26, 9, 30, 15, 250, DateTimeKind.Utc);

    private static string Line(DateTime at, string text) => $"{at.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff} {text}{Environment.NewLine}";

    [Fact]
    public void only_the_first_line_of_a_batch_wakes_the_writer()
    {
        var lines = new LogLines();

        Assert.True(lines.Add(At, "one"));
        Assert.False(lines.Add(At, "two"));
        Assert.False(lines.Add(At, "three"));
    }

    [Fact]
    public void a_batch_holds_every_line_in_order_with_the_time_it_was_added()
    {
        var lines = new LogLines();
        var later = At.AddSeconds(2);

        lines.Add(At, "one");
        lines.Add(later, "two");

        Assert.Equal(Line(At, "one") + Line(later, "two"), lines.TakeBatch());
    }

    [Fact]
    public void the_time_is_written_in_local_time()
    {
        var lines = new LogLines();

        lines.Add(At, "one");

        Assert.StartsWith(At.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff") + " one", lines.TakeBatch());
    }

    [Fact]
    public void nothing_queued_gives_no_batch()
    {
        Assert.Null(new LogLines().TakeBatch());
    }

    [Fact]
    public void a_taken_line_is_not_written_again_and_the_next_line_wakes_the_writer()
    {
        var lines = new LogLines();

        lines.Add(At, "one");
        lines.TakeBatch();

        Assert.Null(lines.TakeBatch());
        Assert.True(lines.Add(At, "two"));
        Assert.Equal(Line(At, "two"), lines.TakeBatch());
    }
}
