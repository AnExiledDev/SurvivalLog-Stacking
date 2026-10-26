using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// Writing every item's stack limit once took 22 to 25 ms in one frame on load. It now runs a slice a frame, so it
/// must still reach its end, in order, however small the slices are.
/// </summary>
public class SlicedJobTests
{
    private static IEnumerable<bool> Steps(List<int> done, int count, int waitAt = -1)
    {
        for (var i = 0; i < count; i++)
        {
            done.Add(i);

            yield return i != waitAt;
        }
    }

    [Fact]
    public void With_time_to_spare_the_whole_job_runs_in_one_slice()
    {
        var done = new List<int>();
        var job = new SlicedJob(Steps(done, 50));

        Assert.True(job.Advance(() => false));
        Assert.True(job.IsDone);
        Assert.Equal(Enumerable.Range(0, 50), done);
    }

    /// <summary>A slice used up at once still runs one step, so the job always moves on and ends.</summary>
    [Fact]
    public void A_spent_slice_runs_one_step_and_the_job_still_finishes()
    {
        var done = new List<int>();
        var job = new SlicedJob(Steps(done, 5));
        var slices = 0;

        while (!job.Advance(() => true))
        {
            slices++;

            Assert.Equal(slices, done.Count);
        }

        Assert.Equal(Enumerable.Range(0, 5), done);
        Assert.True(slices >= 4);
    }

    [Fact]
    public void A_slice_stops_once_its_time_is_used()
    {
        var done = new List<int>();
        var job = new SlicedJob(Steps(done, 10));
        var checks = 0;

        Assert.False(job.Advance(() => ++checks >= 3));
        Assert.Equal(3, done.Count);

        Assert.True(job.Advance(() => false));
        Assert.Equal(Enumerable.Range(0, 10), done);
    }

    [Fact]
    public void A_step_that_waits_ends_the_slice_even_with_time_left()
    {
        var done = new List<int>();
        var job = new SlicedJob(Steps(done, 6, waitAt: 2));

        Assert.False(job.Advance(() => false));
        Assert.Equal(3, done.Count);

        Assert.True(job.Advance(() => false));
        Assert.Equal(6, done.Count);
    }

    [Fact]
    public void A_finished_job_runs_nothing_more()
    {
        var done = new List<int>();
        var job = new SlicedJob(Steps(done, 2));

        job.Advance(() => false);

        Assert.True(job.Advance(() => false));
        Assert.Equal(2, done.Count);
    }
}
