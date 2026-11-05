using Jint;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The split picker follows Exiled's Toolkit's UI size: picker.js hands its backdrop to the Toolkit's page API (api.js,
/// from the Toolkit's source), run in Jint on a fake root page (PickerFake.js). The backdrop fills the screen at any
/// size, so the plate inside it is what must fit. The game can run either script first.
/// </summary>
public class PickerUiSizeTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_picker_is_handed_over_once_with_its_plate_to_fit(bool isToolkitFirst)
    {
        var page = new Page(isToolkitFirst);

        page.Run("__slstacking.open(1, 20, 10); __slstacking.open(2, 20, 10)");
        page.LoadToolkitIfLate();

        Assert.Equal(1, page.Number("handed.length"));
        Assert.Equal("Stacking", page.Text("handed[0].options.mod"));
        Assert.Equal(("sls-root", "sls-plate"), (page.Text("handed[0].el.id"), page.Text("handed[0].options.fit.className")));
        Assert.True(page.Bool("handed[0].options.fit === pickerRoot().children[0]"));
    }

    /// <summary>A picker opened before the Toolkit's page API loads is drawn plain until it arrives, then handed over.</summary>
    [Fact]
    public void A_picker_opened_before_the_Toolkit_loads_is_handed_over_when_it_does()
    {
        var page = new Page(isToolkitFirst: false);

        page.Run("__slstacking.open(1, 20, 10)");

        Assert.Equal(0, page.Number("handed.length"));

        page.LoadToolkitIfLate();

        Assert.Equal(1, page.Number("handed.length"));
        Assert.True(page.Bool("handed[0].el === pickerRoot()"));
    }

    /// <summary>The Toolkit lets go of a panel taken out of its page, so a picker built again must be handed again.</summary>
    [Fact]
    public void A_picker_built_again_after_the_page_lost_it_is_handed_over_again()
    {
        var page = new Page();
        page.Run("__slstacking.open(1, 20, 10); var first = pickerRoot(); first.remove()");

        page.Run("__slstacking.open(2, 20, 10)");

        Assert.Equal(2, page.Number("handed.length"));
        Assert.True(page.Bool("handed[1].el === pickerRoot() && handed[1].el !== first"));
    }

    [Fact]
    public void Before_the_Toolkits_UI_size_script_loads_the_picker_waits_for_it()
    {
        var page = new Page(before: "delete window.ExiledUi");

        page.Run("__slstacking.open(1, 20, 10)");

        Assert.Equal(1, page.Number("window.__exiledUiWait.length"));
        Assert.Equal("sls-root", page.Text("window.__exiledUiWait[0][0].id"));
        Assert.Equal("Stacking", page.Text("window.__exiledUiWait[0][1].mod"));
    }

    /// <summary>Messages go through the Toolkit's toasts now, so the page draws none of its own.</summary>
    [Fact]
    public void The_page_has_no_toast_of_its_own()
    {
        var page = new Page();

        page.Run("__slstacking.open(1, 20, 10)");

        Assert.False(page.Bool("window.__slstacking.toast"));
        Assert.Equal(1, page.Number("handed.length"));
    }

    /// <summary>
    /// picker.js on PickerFake.js, after api.js or, with <paramref name="isToolkitFirst"/> false, before it until
    /// <see cref="LoadToolkitIfLate"/>; <paramref name="before"/> runs ahead of both scripts.
    /// </summary>
    private sealed class Page
    {
        private readonly Engine _engine = new();
        private bool _isToolkitLoaded;

        public Page(bool isToolkitFirst = true, string? before = null)
        {
            _engine.Execute(Resource("Stacking.Tests.PickerFake.js"));
            if (before != null) _engine.Execute(before);
            if (isToolkitFirst) LoadToolkitIfLate();
            _engine.Execute(Resource("Stacking.picker.js"));
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
            using var stream = typeof(PickerUiSizeTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
