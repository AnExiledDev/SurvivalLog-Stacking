using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using GameCore.HotUpdate;
using HarmonyLib;
using Stacking.Adapters;
using Stacking.Core;

namespace Stacking;

[BepInPlugin(Guid, "Stacking", Version)]
public sealed class Plugin : BasePlugin
{
    private const string Guid = "com.dev.survivallog.stacking";
    private const string Version = "0.3.1";

    /// <summary>Seeds and food are the two players ask to stack by name; every other category keeps the game's limit until set.</summary>
    private static readonly Dictionary<ItemCategory, int> DefaultLimits = new() { [ItemCategory.Seed] = 99, [ItemCategory.Food] = 20 };

    public override void Load()
    {
        var wantedByCategory = BindLimits();
        var isCountingWholeStacks = Config.Bind("Cooking", "CountWholeStacks", true,
            "A cooking bench counts every unit of a stack as an ingredient, as it counts loose items, and a finished cook uses up the units its dishes took. Off: a stack counts as one ingredient, as without this mod.").Value;
        var isSplitting = Config.Bind("Splitting", "Enabled", true,
            "Drop a stack with Shift held to move half of it, Ctrl to move one, Ctrl+Shift to choose how many. Right-click a stack with the same keys to split it onto the nearest free spot in its own bag. Off: a drag moves the whole stack and a right-click does what the game does, as without this mod.").Value;
        var harmony = new Harmony(Guid);
        var log = new StackLog(Log, Path.Combine(Paths.BepInExRootPath, "stacking.log"));

        log.TrimIfLarge();
        log.Write($"===== Stacking {Version} started: {Describe(wantedByCategory)}");

        var canGuardFreshness = StackGuard.Apply(harmony, log.Write, log.WriteFileOnly);

        if (isCountingWholeStacks) Cooking.Apply(harmony, log.Write);
        else log.Write("cooking counts a stack as one ingredient: Cooking/CountWholeStacks is off");

        // Only items this mod made stackable are kept out of stations, and none are made stackable without the guard.
        // The units kept out must start a stack of their own, so the station rule needs NewStacks, splitting or not.
        if (canGuardFreshness) KeepStacksOutOfStations(harmony, log);

        StackTicker.Log = log.Write;
        StackTicker.Writer = new LimitWriter(wantedByCategory, canGuardFreshness, log.Write, log.WriteFileOnly);
        AddComponent<StackTicker>();

        if (!isSplitting) log.Write("stacks are not split: Splitting/Enabled is off");
        else if (StackSplitting.Apply(harmony, log.Write, log.WriteFileOnly))
        {
            SplitTicker.Log = log.Write;
            AddComponent<SplitTicker>();
        }
    }

    private static void KeepStacksOutOfStations(Harmony harmony, StackLog log)
    {
        if (!NewStacks.Apply(harmony, log.Write))
        {
            log.Write("stacks can go into cooking and crafting stations whole this session: the units kept out could not start a stack of their own");

            return;
        }

        var mover = new StationMover(log.Write, log.WriteFileOnly);

        if (StationGuard.Apply(harmony, mover, log.Write, log.WriteFileOnly)) StackTicker.Mover = mover;
    }

    private Dictionary<int, int> BindLimits()
    {
        var wanted = new Dictionary<int, int>();

        foreach (var category in Enum.GetValues(typeof(ItemCategory)).Cast<ItemCategory>().Where(c => c != ItemCategory.None))
        {
            DefaultLimits.TryGetValue(category, out var fallback);

            var limit = StackLimits.Clamp(Config.Bind("StackLimits", category.ToString(), fallback,
                $"Most {category} items in one stack, up to {StackLimits.Highest}. 0, or anything at or below the game's own limit, keeps the game's limit. Items with portions or several uses always keep theirs. BepInEx/stacking.log lists the game's limits per category.").Value);

            if (limit > 0) wanted[(int)category] = limit;
        }

        return wanted;
    }

    private static string Describe(Dictionary<int, int> wantedByCategory) =>
        wantedByCategory.Count == 0
            ? "no limits set, the game's limits stand"
            : string.Join(", ", wantedByCategory.Select(pair => $"{(ItemCategory)pair.Key} {pair.Value}"));
}
