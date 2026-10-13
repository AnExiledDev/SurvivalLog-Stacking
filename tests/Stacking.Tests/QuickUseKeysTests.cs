using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class QuickUseKeysTests
{
    private const int LeftAlt = 308;
    private const int RightAlt = 307;
    private const int Z = 122;
    private const int LeftShift = 304;
    private const int RightShift = 303;
    private const int LeftControl = 306;
    private const int RightControl = 305;

    /// <summary>A few of Unity's key names with their real numbers, matched as Unity's own parse matches them, ignoring case.</summary>
    private static readonly Dictionary<string, int> UnityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["None"] = 0,
        ["Z"] = Z,
        ["RightShift"] = RightShift,
        ["LeftShift"] = LeftShift,
        ["RightControl"] = RightControl,
        ["LeftControl"] = LeftControl,
        ["RightAlt"] = RightAlt,
        ["LeftAlt"] = LeftAlt,
        ["Mouse0"] = 323,
        ["Mouse6"] = 329,
    };

    private static int? KeyCodeOf(string name) => UnityKeys.TryGetValue(name, out var code) ? code : null;

    private static QuickUseKeySetting Parse(string? text) => QuickUseKeys.Parse(text, KeyCodeOf);

    [Fact]
    public void The_default_moves_quick_use_to_left_alt()
    {
        Assert.Equal(new QuickUseKeySetting(new QuickUseKey.Key(LeftAlt), null), Parse(QuickUseKeys.DefaultName));
    }

    [Theory]
    [InlineData("Ctrl")]
    [InlineData(" ctrl ")]
    [InlineData("LeftControl")]
    [InlineData("rightcontrol")]
    public void Ctrl_gives_quick_use_back_to_the_game(string text)
    {
        Assert.Equal(new QuickUseKeySetting(new QuickUseKey.GameCtrl(), null), Parse(text));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void None_or_nothing_turns_quick_use_off(string? text)
    {
        Assert.Equal(new QuickUseKeySetting(new QuickUseKey.Off(), null), Parse(text));
    }

    [Fact]
    public void Any_other_unity_key_takes_over_quick_use()
    {
        Assert.Equal(new QuickUseKeySetting(new QuickUseKey.Key(Z), null), Parse("z"));
    }

    [Fact]
    public void A_name_unity_does_not_know_falls_back_to_the_default_and_says_which_name()
    {
        var setting = Parse("Alt");

        Assert.Equal(new QuickUseKey.Key(LeftAlt), setting.Key);
        Assert.Contains("'Alt'", setting.Problem);
    }

    [Theory]
    [InlineData("LeftShift")]
    [InlineData("RightShift")]
    public void Shift_is_refused_because_it_splits_stacks(string text)
    {
        var setting = Parse(text);

        Assert.Equal(new QuickUseKey.Key(LeftAlt), setting.Key);
        Assert.Contains("Shift", setting.Problem);
    }

    [Theory]
    [InlineData("Mouse0")]
    [InlineData("Mouse6")]
    public void A_mouse_button_is_refused_because_it_is_the_click_itself(string text)
    {
        var setting = Parse(text);

        Assert.Equal(new QuickUseKey.Key(LeftAlt), setting.Key);
        Assert.Contains("mouse button", setting.Problem);
    }

    [Theory]
    [InlineData(LeftControl)]
    [InlineData(RightControl)]
    public void A_ctrl_read_in_the_bag_answers_for_the_chosen_key(int asked)
    {
        Assert.Equal(new KeyRead.Instead(LeftAlt), QuickUseKeys.ReadFor(new QuickUseKey.Key(LeftAlt), asked));
    }

    [Theory]
    [InlineData(LeftShift)]
    [InlineData(RightShift)]
    [InlineData(LeftAlt)]
    [InlineData(Z)]
    public void Other_key_reads_are_left_alone_so_shift_still_quick_moves(int asked)
    {
        Assert.Equal(new KeyRead.AsAsked(), QuickUseKeys.ReadFor(new QuickUseKey.Key(LeftAlt), asked));
    }

    [Fact]
    public void With_quick_use_off_ctrl_never_reads_as_held()
    {
        Assert.Equal(new KeyRead.NotHeld(), QuickUseKeys.ReadFor(new QuickUseKey.Off(), LeftControl));
    }

    [Fact]
    public void With_the_game_s_ctrl_a_ctrl_read_is_left_alone()
    {
        Assert.Equal(new KeyRead.AsAsked(), QuickUseKeys.ReadFor(new QuickUseKey.GameCtrl(), RightControl));
    }

    [Fact]
    public void Any_key_a_setting_can_choose_never_answers_a_ctrl_read_with_ctrl()
    {
        foreach (var name in UnityKeys.Keys)
        {
            if (Parse(name).Key is not QuickUseKey.Key chosen) continue;

            Assert.False(QuickUseKeys.IsCtrl(chosen.Code), $"{name} would make a Ctrl read ask for Ctrl again");
        }
    }
}
