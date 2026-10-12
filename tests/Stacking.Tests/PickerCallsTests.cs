using System.Text.Json;
using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

public class PickerCallsTests
{
    /// <summary>A JSON string literal is a JavaScript one, so a text that survives a JSON read survives the page.</summary>
    [Theory]
    [InlineData("That stack is already full")]
    [InlineData("Say \"no\"")]
    [InlineData("C:\\saves\\one")]
    [InlineData("two\nlines\r\tand a tab")]
    [InlineData("ends with a backslash\\")]
    [InlineData("line\u2028separator")]
    public void A_toast_shows_the_exact_text_whatever_it_contains(string text)
    {
        var literal = PickerCalls.Quote(text);

        Assert.Equal(text, JsonSerializer.Deserialize<string>(literal));
        Assert.DoesNotContain('\n', literal);
        Assert.DoesNotContain('\u2028', literal);
    }

    [Fact]
    public void The_picker_opens_with_its_token_the_most_it_may_split_and_the_amount_it_suggests()
    {
        Assert.Equal("window.__slstacking.open(4,19,10)", PickerCalls.Open(4, 19, 10));
    }

    /// <summary>A page that reloaded has no picker script, so the call itself must answer gone for it.</summary>
    [Fact]
    public void A_check_asks_the_picker_and_answers_gone_itself_when_the_page_lost_the_script()
    {
        Assert.Equal("window.__slstacking?window.__slstacking.check(4):window.vuplex&&window.vuplex.postMessage(\"slmod|stacking|gone|4\")",
            PickerCalls.Check(4));
    }
}
