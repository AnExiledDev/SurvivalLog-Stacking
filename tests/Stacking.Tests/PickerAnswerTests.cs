using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class PickerAnswerTests
{
    [Fact]
    public void A_split_answer_carries_the_picker_it_answers_and_how_many_units()
    {
        Assert.Equal(new PickerAnswer.Split(7, 12), PickerAnswer.Parse("slmod|stacking|split|7|12"));
    }

    [Fact]
    public void A_cancel_answer_carries_the_picker_it_closes()
    {
        Assert.Equal(new PickerAnswer.Cancel(7), PickerAnswer.Parse("slmod|stacking|cancel|7"));
    }

    [Fact]
    public void The_page_saying_the_picker_is_up_carries_the_picker_it_means()
    {
        Assert.Equal(new PickerAnswer.Shown(7), PickerAnswer.Parse("slmod|stacking|shown|7"));
    }

    [Fact]
    public void The_page_saying_the_picker_is_gone_carries_the_picker_it_means()
    {
        Assert.Equal(new PickerAnswer.Gone(7), PickerAnswer.Parse("slmod|stacking|gone|7"));
    }

    [Fact]
    public void The_script_installing_itself_says_the_page_is_ready()
    {
        Assert.Equal(new PickerAnswer.Ready(), PickerAnswer.Parse("slmod|stacking|ready"));
    }

    [Fact]
    public void Another_mod_s_page_message_is_not_ours_to_swallow()
    {
        Assert.False(PickerAnswer.IsOurs("slmod|storagehub|close"));
    }

    [Theory]
    [InlineData("slmod|stacking|split|7|0")]
    [InlineData("slmod|stacking|split|7|-3")]
    [InlineData("slmod|stacking|split|7|2.5")]
    [InlineData("slmod|stacking|split|7")]
    [InlineData("slmod|stacking|split|x|3")]
    [InlineData("slmod|stacking|cancel")]
    [InlineData("slmod|stacking|explode|7")]
    [InlineData("slmod|stacking|shown")]
    [InlineData("slmod|stacking|gone|x")]
    [InlineData("slmod|stacking|ready|7")]
    public void A_malformed_answer_moves_nothing(string message)
    {
        Assert.IsType<PickerAnswer.Unknown>(PickerAnswer.Parse(message));
    }
}
