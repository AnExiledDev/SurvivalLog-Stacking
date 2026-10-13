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
    private const string Version = "0.4.1";

    public override void Load()
    {
        var wantedByCategory = BindLimits();
        var isCountingWholeStacks = Config.Bind("Cooking", "CountWholeStacks", true,
            "On: a cooking bench counts every item in a stack as an ingredient, like loose items, and the finished cook uses up the items its dishes took. Off: a stack counts as one ingredient, as in the base game.\n"
            + "开启：烹饪家具把堆叠里的每一件都算作食材，和散放的物品一样，烹饪完成时扣除菜品用掉的数量。关闭：一个堆叠只算一份食材，与原版游戏相同。").Value;
        var isSplitting = Config.Bind("Splitting", "Enabled", true,
            "Hold Shift while you drop a stack to move half, Ctrl to move one, or Ctrl+Shift to choose how many; a right-click with the same keys splits the stack onto the nearest free spot in the same bag or storage. Off: drags and right-clicks work as in the base game.\n"
            + "拖拽放下堆叠时按住 Shift 移动一半，按住 Ctrl 移动 1 件，按住 Ctrl+Shift 自选数量；按住同样的键右键点击，会把堆叠拆分到同一背包或储物空间里最近的空位。关闭：拖拽和右键与原版游戏相同。").Value;
        var quickUseKey = Config.Bind("Keys", "QuickUseKey", QuickUseKeys.DefaultName,
            "The key to hold while you click an item in the bag to use it at once, which the game puts on Ctrl. Holding it also lights up the items that can be used that way. With it off Ctrl, Ctrl in the bag is only for splitting stacks. Ctrl gives quick use back to the game, and None turns it off. Any Unity key name works, such as LeftAlt, RightAlt or Z; Shift and the mouse buttons cannot be used. LeftAlt is also the game's default highlight key: with the bag open beside a storage, trunk or second bag, a click with it held sends every item of that kind across, so quick use there needs a different key.\n"
            + "在背包里按住此键点击物品可立即使用，游戏默认用 Ctrl。按住时也会高亮可以这样使用的物品。改用其他键后，背包里的 Ctrl 只用于拆分堆叠。填 Ctrl 交还给游戏，填 None 关闭快速使用。可填任意 Unity 按键名，例如 LeftAlt、RightAlt 或 Z；不能用 Shift 和鼠标按键。LeftAlt 也是游戏默认的高亮键：背包与储物空间、后备箱或第二个背包并排打开时，按住它点击会把同类物品全部送到另一边，所以在那里快速使用需要换一个键。").Value;
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

        QuickUseKeyHooks.Apply(harmony, quickUseKey, log.Write);
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
            var shown = StackCategories.For((int)category, category.ToString());
            var limit = StackLimits.Clamp(Config.Bind("StackLimits", category.ToString(), shown.Default, StackCategories.Describe(shown)).Value);

            if (limit > 0) wanted[(int)category] = limit;
        }

        return wanted;
    }

    private static string Describe(Dictionary<int, int> wantedByCategory) =>
        wantedByCategory.Count == 0
            ? "no limits set, the game's limits stand"
            : string.Join(", ", wantedByCategory.Select(pair => $"{(ItemCategory)pair.Key} {pair.Value}"));
}
