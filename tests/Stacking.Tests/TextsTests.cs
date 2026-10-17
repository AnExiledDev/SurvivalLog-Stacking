using System.Linq;
using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>Every word the mod shows has an English and a Chinese version, and the game's language picks between them.</summary>
public class TextsTests
{
    public static TheoryData<int> Entries()
    {
        var data = new TheoryData<int>();

        foreach (var index in Enumerable.Range(0, Texts.All.Count)) data.Add(index);

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void Every_text_has_plain_english_and_a_real_chinese_translation(int index)
    {
        var phrase = Texts.All[index];

        Assert.False(string.IsNullOrWhiteSpace(phrase.En));
        Assert.True(phrase.En.All(ch => ch is >= ' ' and <= '~'), phrase.En);
        Assert.Contains(phrase.Zh, ch => ch is >= '\u4e00' and <= '\u9fff');
        Assert.NotEqual(phrase.En, phrase.Zh);
        Assert.Equal(phrase.En.Contains("{0}"), phrase.Zh.Contains("{0}"));
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void The_games_language_picks_the_text(int index)
    {
        var phrase = Texts.All[index];

        Assert.Equal(phrase.Zh, phrase.Pick(isChinese: true));
        Assert.Equal(phrase.En, phrase.Pick(isChinese: false));
    }

    [Fact]
    public void No_text_is_listed_twice()
    {
        Assert.Equal(Texts.All.Count, Texts.All.Distinct().Count());
    }
}
