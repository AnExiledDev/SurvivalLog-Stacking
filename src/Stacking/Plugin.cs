using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using GameCore.HotUpdate;
using HarmonyLib;
using Stacking.Adapters;
using Stacking.Core;

namespace Stacking;

[BepInPlugin(Guid, "Stacking", Version)]
[BepInDependency(ToolkitGuid, ">=0.19.0")]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.stacking";
    private const string Version = "0.8.4";
    private const string ToolkitGuid = "com.dev.survivallog.exiledtoolkit";
    private const int MaxLoggedFailures = 3;

    /// <summary>Stack decision lines add up over a long session, so past 1 MB the file starts again.</summary>
    private const long MaxLogFileBytes = 1024 * 1024;

    public override void Load()
    {
        var limitEntries = BindLimits();

        // A cook already counted one way has to finish the same way, so the cooking rule waits for the next start.
        ModUiSize.Bind(Config, "Stacking");

        var isCountingWholeStacks = Config.Bind("Cooking", "CountWholeStacks", true, new ConfigDescription(
            "On: a cooking bench counts every item in a stack as an ingredient, like loose items, and the finished cook uses up the items its dishes took. Off: a stack counts as one ingredient, as in the base game. Takes effect at the next game start.\n"
            + "开启：烹饪家具把堆叠里的每一件都算作食材，和散放的物品一样，烹饪完成时扣除菜品用掉的数量。关闭：一个堆叠只算一份食材，与原版游戏相同。下次启动游戏时生效。", null, "ModManager.Restart")).Value;

        // Off means the game's own drags and right-clicks, which only an unpatched game gives, so this waits for the next start.
        var isSplitting = Config.Bind("Splitting", "Enabled", true, new ConfigDescription(
            "Hold Shift while you drop a stack to move half, Ctrl to move one, or Ctrl+Shift to choose how many; a right-click with the same keys splits the stack onto the nearest free spot in the same bag or storage. Off: drags and right-clicks work as in the base game. Takes effect at the next game start.\n"
            + "拖拽放下堆叠时按住 Shift 移动一半，按住 Ctrl 移动 1 件，按住 Ctrl+Shift 自选数量；按住同样的键右键点击，会把堆叠拆分到同一背包或储物空间里最近的空位。关闭：拖拽和右键与原版游戏相同。下次启动游戏时生效。", null, "ModManager.Restart")).Value;
        var log = new ModLog(Log, "stacking.log") { MaxFileBytes = MaxLogFileBytes };
        var hooks = new PatchKit(new Harmony(Guid), log.Write, MaxLoggedFailures);

        log.Write($"===== Stacking {Version} started");

        var canGuardFreshness = StackGuard.Apply(hooks, log.Write, log.WriteFileOnly);

        BagAddFreshness.Apply(hooks, log.Write, log.WriteFileOnly);

        if (isCountingWholeStacks) Cooking.Apply(hooks, log.Write);
        else log.Write("cooking counts a stack as one ingredient: Cooking/CountWholeStacks is off");

        // Only items this mod made stackable are kept out of stations, and none are made stackable without the guard.
        // The units kept out must start a stack of their own, so the station rule needs NewStacks, splitting or not.
        if (canGuardFreshness) KeepStacksOutOfStations(hooks, log);

        // Any stack can reach the shredder hopper, the game's own stacks too, so this does not wait on the guard.
        ShredderStacks.Apply(hooks, log.Write, log.WriteFileOnly);

        StackTicker.Log = log.Write;
        var writer = new LimitWriter(canGuardFreshness, log.Write, log.WriteFileOnly);

        LiveSettings.Watch(() =>
        {
            var wantedByCategory = WantedLimits(limitEntries);

            writer.UseLimits(wantedByCategory);
            log.Write($"stack limits: {Describe(wantedByCategory)}");
        }, limitEntries.Values.ToArray<ConfigEntryBase>());
        StackTicker.Writer = writer;
        AddComponent<StackTicker>();

        if (ItemDetailHooks.Apply(hooks)) log.Write("a Ctrl-click on an item opens no item details");

        if (!isSplitting) log.Write("stacks are not split: Splitting/Enabled is off");
        else if (StackSplitting.Apply(hooks, log.Write, log.WriteFileOnly))
        {
            SplitTicker.Log = log.Write;
            AddComponent<SplitTicker>();
        }
    }

    private static void KeepStacksOutOfStations(PatchKit hooks, ModLog log)
    {
        if (!NewStacks.Apply(hooks))
        {
            log.Write("stacks can go into cooking and crafting stations whole this session: the units kept out could not start a stack of their own");

            return;
        }

        var mover = new StationMover(log.Write, log.WriteFileOnly);

        if (!StationGuard.Apply(hooks, mover, log.Write, log.WriteFileOnly)) return;

        StackTicker.Mover = mover;

        // Station moves and the crafting fill hold the station screens' redraws to one per run of moves.
        ScreenRefresh.Apply(hooks, log.Write);
        CraftingFill.Apply(hooks, mover, log.Write, log.WriteFileOnly);
    }

    private Dictionary<int, ConfigEntry<int>> BindLimits()
    {
        var entries = new Dictionary<int, ConfigEntry<int>>();

        foreach (var category in Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().Where(c => c != ItemCategory.None))
        {
            var shown = StackCategories.For((int)category, category.ToString());

            entries[(int)category] = Config.Bind("StackLimits", category.ToString(), StackLimits.KeepGame, StackCategories.Describe(shown));
        }

        return entries;
    }

    private static Dictionary<int, int> WantedLimits(Dictionary<int, ConfigEntry<int>> entries)
    {
        var wanted = new Dictionary<int, int>();

        foreach (var (category, entry) in entries)
        {
            var limit = StackLimits.Clamp(entry.Value);

            if (limit > 0) wanted[category] = limit;
        }

        return wanted;
    }

    private static string Describe(Dictionary<int, int> wantedByCategory) =>
        wantedByCategory.Count == 0
            ? "no limits set, the game's limits stand"
            : string.Join(", ", wantedByCategory.Select(pair => $"{(ItemCategory)pair.Key} {pair.Value}"));
}
