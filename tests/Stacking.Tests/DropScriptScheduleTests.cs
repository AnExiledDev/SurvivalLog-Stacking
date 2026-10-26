using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// drops.js used to go to the web UI whole every three seconds, which the page then parsed each time. Now a short
/// check goes instead, and the whole script only at the start, when the page says it lost it, or when a send did not
/// reach the page, so a reload of the web UI still gets the watcher back.
/// </summary>
public class DropScriptScheduleTests
{
    private const float Every = DropScriptSchedule.CheckSeconds;

    [Fact]
    public void The_script_goes_at_the_first_frame()
    {
        Assert.Equal(DropScriptSend.Script, new DropScriptSchedule().Next(0f));
    }

    [Fact]
    public void Nothing_goes_between_checks()
    {
        var schedule = new DropScriptSchedule();

        schedule.Next(10f);

        Assert.Equal(DropScriptSend.Nothing, schedule.Next(10f + Every / 2));
        Assert.Equal(DropScriptSend.Nothing, schedule.Next(10f + Every - 0.01f));
    }

    /// <summary>An unchanged page gets only the short check, every time, never the whole script again.</summary>
    [Fact]
    public void A_page_that_keeps_the_script_gets_only_the_check()
    {
        var schedule = new DropScriptSchedule();

        schedule.Next(0f);

        for (var i = 1; i <= 20; i++) Assert.Equal(DropScriptSend.Check, schedule.Next(i * Every));
    }

    [Fact]
    public void A_page_that_lost_the_script_gets_it_at_the_next_frame()
    {
        var schedule = new DropScriptSchedule();

        schedule.Next(0f);
        schedule.Next(Every);
        schedule.Missing();

        Assert.Equal(DropScriptSend.Script, schedule.Next(Every + 0.5f));
        Assert.Equal(DropScriptSend.Nothing, schedule.Next(Every + 1f));
        Assert.Equal(DropScriptSend.Check, schedule.Next(2 * Every + 0.5f));
    }

    /// <summary>With no web UI yet the script reached no page, so the next send is the script, not a check that would find nothing to answer it.</summary>
    [Fact]
    public void A_send_that_reached_no_page_is_the_script_again()
    {
        var schedule = new DropScriptSchedule();

        schedule.Next(0f);
        schedule.NotSent();

        Assert.Equal(DropScriptSend.Nothing, schedule.Next(1f));
        Assert.Equal(DropScriptSend.Script, schedule.Next(Every));
        Assert.Equal(DropScriptSend.Check, schedule.Next(2 * Every));
    }

    [Fact]
    public void A_check_that_reached_no_page_is_followed_by_the_script()
    {
        var schedule = new DropScriptSchedule();

        schedule.Next(0f);
        Assert.Equal(DropScriptSend.Check, schedule.Next(Every));
        schedule.NotSent();

        Assert.Equal(DropScriptSend.Script, schedule.Next(2 * Every));
    }

    /// <summary>drops.js sets this flag as it installs, so the check must look for exactly it, and say what the plugin parses.</summary>
    [Fact]
    public void The_check_looks_for_the_script_s_own_flag_and_asks_in_words_the_plugin_reads()
    {
        Assert.StartsWith("window.__slstackingDrops||", DropScriptSchedule.Check);
        Assert.Contains("postMessage('slmod|stacking|drops')", DropScriptSchedule.Check);
        Assert.IsType<PickerAnswer.DropsMissing>(PickerAnswer.Parse("slmod|stacking|drops"));
    }
}
