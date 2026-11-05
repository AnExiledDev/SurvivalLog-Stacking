using Jint;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// drops.js finds the station and trade pages through Exiled's Toolkit's page watch (api.js, from the Toolkit's
/// source), run in Jint on a fake root page (DropsFake.js). Each page gets one mouse-up listener ahead of the game's
/// bag script, and a drop onto another item reaches the plugin. The game can run either script first.
/// </summary>
public class DropWatchPageTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_station_page_already_shown_is_watched_once_whichever_script_loads_first(bool isToolkitFirst)
    {
        var page = new Page(isToolkitFirst, "var bench = addFrame('https://game/Pages/ToolTable.html?t=1'); var hud = addFrame('https://game/Pages/CoreUI1.html')");

        page.LoadToolkitIfLate();
        page.Run("tick(); tick()");

        Assert.Equal(1, page.Number("mouseUps(bench)"));
        Assert.Equal(0, page.Number("mouseUps(hud)"));
    }

    [Fact]
    public void The_trade_window_is_watched_too()
    {
        var page = new Page();

        page.Run("var trade = addFrame('https://game/Pages/TradeUI.html'); tick()");

        Assert.Equal(1, page.Number("mouseUps(trade)"));
    }

    /// <summary>The game loads a new document into a frame it reuses; the new page needs its own listener.</summary>
    [Fact]
    public void A_page_loaded_into_the_same_frame_is_watched_again()
    {
        var page = new Page(true, "var frame = addFrame('https://game/Pages/ToolTable.html'); var first = frame.contentWindow");

        page.Run("tick(); show(frame, 'https://game/Pages/Cooking.html'); tick(); tick()");

        Assert.True(page.Bool("frame.contentWindow !== first"));
        Assert.Equal(1, page.Number("mouseUps(frame)"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Letting_a_stack_go_over_another_item_tells_the_plugin_which_onto_which(bool isToolkitFirst)
    {
        var page = new Page(isToolkitFirst, "var bench = addFrame('https://game/Pages/BrewPanel.html')");

        page.LoadToolkitIfLate();
        page.Run("dragOnto(bench, 12, 34); letGo(bench)");

        Assert.Equal("slmod|stacking|onto|12|34", page.Text("sent.join(',')"));
    }

    /// <summary>The plugin's check asks for the script only while the page lacks it, so it must count as there at once.</summary>
    [Fact]
    public void Before_the_Toolkit_loads_the_script_already_counts_as_installed_and_watches_nothing()
    {
        var page = new Page(isToolkitFirst: false, "var bench = addFrame('https://game/Pages/ToolTable.html')");

        page.Run("tick()");

        Assert.True(page.Bool("window.__slstackingDrops"));
        Assert.Equal(0, page.Number("mouseUps(bench)"));
    }

    /// <summary>
    /// drops.js on DropsFake.js, after api.js or, with <paramref name="isToolkitFirst"/> false, before it until
    /// <see cref="LoadToolkitIfLate"/>; <paramref name="before"/> runs ahead of both scripts.
    /// </summary>
    private sealed class Page
    {
        private readonly Engine _engine = new();
        private bool _isToolkitLoaded;

        public Page(bool isToolkitFirst = true, string? before = null)
        {
            _engine.Execute(Resource("Stacking.Tests.DropsFake.js"));
            if (before != null) _engine.Execute(before);
            if (isToolkitFirst) LoadToolkitIfLate();
            _engine.Execute(Resource("Stacking.drops.js"));
        }

        /// <summary>The Toolkit's page API arriving, unless it already has.</summary>
        public void LoadToolkitIfLate()
        {
            if (_isToolkitLoaded) return;

            _engine.Execute(Resource("ExiledToolkit.api.js"));
            _isToolkitLoaded = true;
        }

        public void Run(string statements) => _engine.Execute(statements);

        public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

        public int Number(string expression) => (int)_engine.Evaluate($"Number({expression})").AsNumber();

        public bool Bool(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

        private static string Resource(string name)
        {
            using var stream = typeof(DropWatchPageTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
