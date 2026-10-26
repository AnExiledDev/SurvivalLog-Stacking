using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using GameCore.HotUpdate;
using Stacking.Core;
using ItemTable = Il2CppSystem.Collections.Generic.Dictionary<int, GameCore.HotUpdate.Config_Item>;
using RecipeTable = Il2CppSystem.Collections.Generic.Dictionary<int, GameCore.HotUpdate.Config_CookingRecipe>;

namespace Stacking.Adapters;

/// <summary>
/// Writes the chosen stack limits onto the game's item table. The table only exists once the game has loaded it,
/// and a reload would bring back the game's limits, so each sweep checks whether a table was replaced or a few raised items lost their limit, and only then rewrites the table.
/// New limits from the settings land at the next sweep, and an item no longer raised gets the game's limit back.
/// A full write reads and writes every item, so it runs a slice of about two milliseconds a frame until it is done.
/// </summary>
internal sealed class LimitWriter
{
    private const double SliceMilliseconds = 2;

    private IReadOnlyDictionary<int, int> _wantedByCategory = new Dictionary<int, int>();
    private readonly bool _canGuardFreshness;
    private readonly Action<string> _log;
    private readonly Action<string> _logFileOnly;

    /// <summary>Each item's limit as the game first defined it, before this mod wrote over it.</summary>
    private readonly Dictionary<int, int> _gameLimits = new();

    /// <summary>The tables the last full write went to, and a few raised items to check between full writes.</summary>
    private TableStamp? _lastStamp;
    private Dictionary<int, int> _canaries = new();
    private readonly List<Canary> _canariesNow = new();

    /// <summary>The full write in progress, and the tables it reads.</summary>
    private SlicedJob? _job;
    private TableStamp _jobStamp;
    private readonly Stopwatch _slice = new();
    private readonly Func<bool> _isSliceSpent;

    private bool _hasApplied;
    private bool _hasReportedWaiting;

    public LimitWriter(bool canGuardFreshness, Action<string> log, Action<string> logFileOnly)
    {
        _canGuardFreshness = canGuardFreshness;
        _log = log;
        _logFileOnly = logFileOnly;
        _isSliceSpent = () => _slice.Elapsed.TotalMilliseconds >= SliceMilliseconds;
    }

    /// <summary>True while a full write is spread over frames; the ticker then calls <see cref="Continue"/> every frame.</summary>
    public bool IsWriting => _job != null;

    /// <summary>
    /// Set on the main thread, where the sweep runs. The next sweep writes them and reports them in full; a write in
    /// progress stops, since it was planned for the old limits.
    /// </summary>
    public void UseLimits(IReadOnlyDictionary<int, int> wantedByCategory)
    {
        _wantedByCategory = wantedByCategory;
        _hasApplied = false;
        _lastStamp = null;
        _job = null;
    }

    /// <summary>Starts a full write when a table was replaced or a watched item lost its limit, and runs its first slice.</summary>
    public void Sweep()
    {
        if (!TryTables(out var config, out var table)) return;

        var stamp = Stamp(config, table);

        if (_lastStamp == stamp && TableWatch.IsUnchanged(_lastStamp, stamp, Canaries(table))) return;

        if (config._Config_CookingRecipe_Dict is not { Count: > 0 } recipes)
        {
            // Writing before then would raise every dish.
            ReportWaitingForRecipes();

            return;
        }

        _jobStamp = stamp;
        _job = new SlicedJob(Steps(table, recipes, stamp));
        Continue();
    }

    /// <summary>
    /// The next slice of the full write. A table replaced since the write began starts it over on the new one, so no
    /// write lands on a table the game no longer reads.
    /// </summary>
    public void Continue()
    {
        if (_job is not { } job) return;

        if (!TryTables(out var config, out var table))
        {
            _job = null;

            return;
        }

        if (Stamp(config, table) != _jobStamp)
        {
            _job = null;
            Sweep();

            return;
        }

        _slice.Restart();

        try
        {
            if (job.Advance(_isSliceSpent)) _job = null;
        }
        catch
        {
            _job = null;

            throw;
        }
    }

    private static bool TryTables([NotNullWhen(true)] out ConfigManager? config, [NotNullWhen(true)] out ItemTable? table)
    {
        config = BaseSingleton<ConfigManager>.IsInstanceCreated ? BaseSingleton<ConfigManager>.Instance : null;
        table = config?._Config_Item_Dict;

        return config != null && table != null && table.Count > 0;
    }

    private static TableStamp Stamp(ConfigManager config, ItemTable table)
    {
        var recipes = config._Config_CookingRecipe_Dict;

        return new TableStamp(table.Pointer.ToInt64(), table.Count, recipes?.Pointer.ToInt64() ?? 0, recipes?.Count ?? 0);
    }

    /// <summary>
    /// The full write, one item a step: the dishes the recipes make, every item's row, the plan, then each limit. The
    /// guard learns of every item about to be raised before the first limit is written.
    /// </summary>
    private IEnumerable<bool> Steps(ItemTable table, RecipeTable recipes, TableStamp stamp)
    {
        var outputs = new List<RecipeOutputs>(recipes.Count);

        foreach (var recipe in recipes.Values)
        {
            if (recipe == null) continue;

            outputs.Add(new RecipeOutputs(recipe._PerfectItemID_k__BackingField, recipe._GoodItemID_k__BackingField, recipe._NormalItemID_k__BackingField, recipe._FailItemID_k__BackingField));

            yield return true;
        }

        var cookedItems = StackLimits.CookedItems(outputs);
        var items = new List<Config_Item>(table.Count);
        var rows = new List<ItemRow>(table.Count);

        foreach (var item in table.Values)
        {
            if (item == null) continue;

            items.Add(item);
            rows.Add(GameRow(item, cookedItems));

            yield return true;
        }

        var plan = StackLimits.Plan(rows, _wantedByCategory, _canGuardFreshness);

        yield return true;

        var neverSpoils = StackLimits.NeverSpoiling(rows);

        yield return true;

        StackGuard.NewlyStackable = StackLimits.GuardedWhileWriting(StackGuard.NewlyStackable, plan.NewlyStackable);
        StackGuard.NeverSpoils = neverSpoils;

        yield return true;

        var written = 0;

        for (var i = 0; i < items.Count; i++)
        {
            if (Write(items[i], StackLimits.LimitFor(plan, rows[i]))) written++;

            yield return true;
        }

        StackGuard.NewlyStackable = plan.NewlyStackable;

        yield return true;

        Report(rows, plan, written, neverSpoils);
        Remember(stamp, plan);
    }

    /// <summary>The watched items as they are now; a missing one reads as limit 0, so it counts as changed. Fills one reused list.</summary>
    private List<Canary> Canaries(ItemTable table)
    {
        _canariesNow.Clear();

        foreach (var (id, written) in _canaries)
        {
            _canariesNow.Add(new Canary(written, table.TryGetValue(id, out var item) && item != null ? item._StackLimit_k__BackingField : 0));
        }

        return _canariesNow;
    }

    private void Remember(TableStamp stamp, StackPlan plan)
    {
        _lastStamp = stamp;
        _canaries = TableWatch.PickCanaries(plan.Limits.Keys).ToDictionary(id => id, id => plan.Limits[id]);
    }

    /// <summary>Each value read straight from the row's fields, which is what its properties return.</summary>
    private ItemRow GameRow(Config_Item item, IReadOnlySet<int> cookedItems)
    {
        var id = item._ID_k__BackingField;

        if (!_gameLimits.TryGetValue(id, out var gameLimit)) _gameLimits[id] = gameLimit = item._StackLimit_k__BackingField;

        return new ItemRow(id, item._Category_k__BackingField, gameLimit, item._UseTimes_k__BackingField, item._Life_k__BackingField, cookedItems.Contains(id), !item._CantUse_k__BackingField);
    }

    private static bool Write(Config_Item item, int limit)
    {
        if (item._StackLimit_k__BackingField == limit) return false;

        item._StackLimit_k__BackingField = limit;

        return true;
    }

    private void ReportWaitingForRecipes()
    {
        if (_hasReportedWaiting) return;

        _hasReportedWaiting = true;
        _log("the item table is loaded but the cooking recipe table is not; stack limits wait for it, so no limit is raised until then");
    }

    private void Report(List<ItemRow> rows, StackPlan plan, int written, IReadOnlySet<int> neverSpoils)
    {
        if (_hasApplied)
        {
            if (written > 0) _log($"the game reloaded its item table, {written} stack limits written again");

            return;
        }

        _hasApplied = true;
        _log($"stack limits written for {written} items, {plan.Limits.Count} raised, {plan.NewlyStackable.Count} of which the game never stacked");
        _log($"{plan.NewlyStackable.Count(neverSpoils.Contains)} of the newly stackable items never spoil, so when they were picked up does not keep them apart");

        // Grouping every item by category is the costliest part of the report and only feeds the log file, so it runs
        // off the main thread; the rows and the plan are not changed after the write.
        Task.Run(() =>
        {
            try
            {
                foreach (var line in StackLimits.Describe(rows, plan, CategoryName)) _logFileOnly(line);
            }
            catch (Exception)
            {
                // The per-category lines are only for the log file; the limits are already written.
            }
        });
    }

    private static string CategoryName(int category) =>
        Enum.IsDefined(typeof(ItemCategory), category) ? ((ItemCategory)category).ToString() : $"category {category}";
}
