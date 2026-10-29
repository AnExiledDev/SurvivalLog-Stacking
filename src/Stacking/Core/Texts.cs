using System.Collections.Generic;

namespace Stacking.Core;

/// <summary>One piece of on-screen text in the two languages the game ships.</summary>
public readonly record struct Phrase(string En, string Zh)
{
    public string Pick(bool isChinese) => isChinese ? Zh : En;
}

/// <summary>The words the mod puts on screen: the split picker and the messages a split can end with.</summary>
public static class Texts
{
    /// <summary>Who a toast is from, on its first line.</summary>
    public static readonly Phrase Source = new("Stacking", "堆叠增强");

    public static readonly Phrase PickerTitle = new("Split stack", "拆分堆叠");

    /// <summary>{0} is the most units the stack can give.</summary>
    public static readonly Phrase PickerHint = new("How many to move: 1 to {0}", "移动数量：1 至 {0}");

    public static readonly Phrase PickerHalf = new("Half", "一半");

    public static readonly Phrase PickerMax = new("Max", "最多");

    public static readonly Phrase PickerCancel = new("Cancel", "取消");

    public static readonly Phrase PickerSplit = new("Split", "拆分");

    public static readonly Phrase NoRoom = new("No room to split the stack there", "那里没有空位拆分堆叠");

    public static readonly Phrase StackFull = new("That stack is already full", "那一堆已经满了");

    public static readonly Phrase TooHeavy = new("That would be too heavy for the container", "放进去会超出这个容器的负重");

    public static readonly Phrase OtherCondition = new("Those don't stack: freshness or condition differs", "这两堆不能叠在一起：新鲜程度或状态不同");

    public static readonly Phrase OwnRules = new("Stacks can't be split into or out of this container; split them in your bag first", "这个容器不能拆入或拆出堆叠，请先在背包里拆分");

    public static readonly Phrase Unreadable = new("Couldn't read this container's layout, so nothing split: see BepInEx/stacking.log", "无法读取这个容器的格子布局，没有拆分任何物品：详见 BepInEx/stacking.log");

    public static readonly Phrase ElsewhereInBag = new("That spot was taken, so the split-off stack went elsewhere in the container", "那个位置已被占用，拆出的一堆放到了容器里的其他位置");

    public static readonly Phrase OnTheGround = new("No room in the container, so the split-off stack was dropped on the ground", "容器里没有空位，拆出的一堆掉在了地上");

    public static readonly Phrase TakeOutOnly = new("Items can only be taken out of this box", "这个箱子里的物品只能取出");

    public static readonly Phrase NotForThisBasket = new("That can't go in this basket", "这件物品不能放进这个篮子");

    public static readonly Phrase NoNewStacks = new("Splitting onto a free spot is off this session: see BepInEx/stacking.log", "本次游戏中无法拆分到空位：详见 BepInEx/stacking.log");

    public static readonly Phrase NoPicker = new("Ctrl+Shift needs the amount picker, which could not start: see BepInEx/stacking.log", "Ctrl+Shift 需要数量选择框，但它没能启动：详见 BepInEx/stacking.log");

    public static readonly Phrase Failed = new("The split failed and nothing moved: see BepInEx/stacking.log", "拆分失败，没有移动任何物品：详见 BepInEx/stacking.log");

    public static IReadOnlyList<Phrase> All { get; } = new[]
    {
        PickerTitle, PickerHint, PickerHalf, PickerMax, PickerCancel, PickerSplit, NoRoom, StackFull, TooHeavy, OtherCondition,
        OwnRules, Unreadable, ElsewhereInBag, OnTheGround, TakeOutOnly, NotForThisBasket, NoNewStacks, NoPicker, Failed,
    };
}
