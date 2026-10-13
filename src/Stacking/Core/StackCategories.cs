using System.Collections.Generic;
using System.Linq;

namespace Stacking.Core;

/// <summary>The lowest and highest stack limit the game's item table gives the items of one category.</summary>
public readonly record struct GameLimit(int Lowest, int Highest)
{
    public string Text(string between) => Lowest == Highest ? $"{Lowest}" : $"{Lowest}{between}{Highest}";
}

/// <summary>
/// One of the game's item categories as its config entry shows it: this mod's default limit, the game's own limit
/// (none for a category the game has no items in), and what the category holds, in English and in Chinese.
/// </summary>
public sealed record StackCategory(int Id, int Default, GameLimit? Game, string Holds, string HoldsChinese);

public static class StackCategories
{
    private static readonly GameLimit One = new(1, 1);

    /// <summary>
    /// Every category of the game's ItemCategory, by its number. The game limits and example items were taken from the
    /// item table of game version 1.1.18317. Furniture, story, quest and other one-off items keep the game's limit;
    /// materials, seeds and everyday consumables stack higher.
    /// </summary>
    public static readonly IReadOnlyList<StackCategory> All = new StackCategory[]
    {
        new(1, 20, One,
            "Stack size for Food, such as Cookies, Bread and Canned Luncheon Meat. Only food with the same freshness shares a stack.",
            "食物的堆叠数量，如饼干、面包、午餐肉罐头。只有新鲜度相同的食物才会堆叠在一起。"),
        new(2, 10, One,
            "Stack size for Meds, such as Vitamins, Bandage and Adhesive Bandage.",
            "药物的堆叠数量，如维生素、急救绷带、创可贴。"),
        new(3, 0, One,
            "Stack size for Books, such as Woodworking Basics and Gardening Manual.",
            "书籍的堆叠数量，如木工入门、园艺手册。"),
        new(4, 0, One,
            "Stack size for Electronics, such as the Game Console.",
            "电器的堆叠数量，如游戏机。"),
        new(5, 10, One,
            "Stack size for Household items, such as Cigarettes, Paper Crane and Paper Airplane.",
            "日用品的堆叠数量，如卷烟、千纸鹤、纸飞机。"),
        new(6, 0, One,
            "Stack size for Tools, such as Lockpick, Crowbar and Pliers.",
            "工具的堆叠数量，如开锁器、撬棍、工具钳。"),
        new(7, 0, null,
            "Stack size for Pets. The game has no items in this category.",
            "宠物的堆叠数量。游戏中没有这一类物品。"),
        new(8, 0, One,
            "Stack size for Packaging, such as the Random Materials Pack (x8).",
            "包裹的堆叠数量，如随机材料包X8。"),
        new(9, 99, One,
            "Stack size for Materials, such as Wood Chips, Scrap Metal and Wire.",
            "材料的堆叠数量，如木片、铁皮、铁丝。"),
        new(10, 99, One,
            "Stack size for Seeds, such as Carrot Seeds, Potato Seeds and Tomato Seeds. Seeds of one kind always share a stack.",
            "种子的堆叠数量，如胡萝卜种子、土豆种子、西红柿种子。同一种种子总会堆叠在一起。"),
        new(11, 20, One,
            "Stack size for Fertilizer, such as Basic Fertilizer and Compound Fertilizer.",
            "肥料的堆叠数量，如基础肥料、复合肥料。"),
        new(12, 0, One,
            "Stack size for Traps, such as Simple Mousetrap and Iron Animal Trap. Traps can be used again and again, so they keep the game's limit.",
            "陷阱的堆叠数量，如简易捕鼠夹、铁制捕兽夹。陷阱可以反复使用，所以保持游戏上限。"),
        new(13, 20, One,
            "Stack size for Fuel, such as Gas Canister, Diesel and Common Alcohol.",
            "燃料的堆叠数量，如卡式气瓶、柴油、普通酒精。"),
        new(14, 0, One,
            "Stack size for Furniture Packages, such as Old Guitar Package and Shredder Package.",
            "家具包裹的堆叠数量，如老吉他包裹、粉碎机包裹。"),
        new(15, 10, One,
            "Stack size for Plants, such as Daisy, Pansy and Rose.",
            "花卉的堆叠数量，如雏菊、三色堇、蔷薇。"),
        new(16, 10, One,
            "Stack size for Throwables, such as Molotov Cocktail and Rotten Meat Strip.",
            "投掷物的堆叠数量，如燃烧瓶、腐肉条。"),
        new(17, 0, One,
            "Stack size for Robot Modules, such as Compact Battery Pack and Harvest Module.",
            "机器人模块的堆叠数量，如轻便电池组、收获模块。"),
        new(18, 0, One,
            "Stack size for Story Items, such as Mysterious Envelope and Note.",
            "剧情道具的堆叠数量，如神秘信封、纸条。"),
        new(19, 0, One,
            "Stack size for Collectibles, such as Gold Bar, Diamond Ring and Artistic Sculpture.",
            "收藏品的堆叠数量，如金条、钻石戒指、艺术雕塑。"),
        new(20, 0, One,
            "Stack size for Records, such as \"The Long Night\" and \"The Way Home\".",
            "唱片的堆叠数量，如唱片《长夜》、唱片《归途》。"),
        new(21, 30, new GameLimit(1, 10),
            "Stack size for Renovation Materials, such as wallpaper and flooring. Dyes can be used again and again, so they keep the game's limit.",
            "装修材料的堆叠数量，如墙纸、地板。染料可以反复使用，所以保持游戏上限。"),
        new(22, 0, One,
            "Stack size for Quest Items, such as Garage Key and Cold Storage Key.",
            "任务道具的堆叠数量，如车库钥匙、冷库钥匙。"),
    };

    /// <summary>
    /// The category with this number, or, for one the game adds later, a plain entry under its key name that keeps the
    /// game's limit.
    /// </summary>
    public static StackCategory For(int id, string key) =>
        All.FirstOrDefault(category => category.Id == id)
        ?? new StackCategory(id, 0, null, $"Stack size for {key} items.", $"{key} 类物品的堆叠数量。");

    /// <summary>The config text: what the category holds, the game's limit and this mod's default, in English then Chinese.</summary>
    public static string Describe(StackCategory category)
    {
        var game = category.Game;
        var limits = game is null
            ? $"Default {category.Default}, highest {StackLimits.Highest}"
            : $"Game limit {game.Value.Text(" to ")}, default {category.Default}, highest {StackLimits.Highest}";
        var limitsChinese = game is null
            ? $"默认 {category.Default}，最高 {StackLimits.Highest}"
            : $"游戏上限 {game.Value.Text(" 至 ")}，默认 {category.Default}，最高 {StackLimits.Highest}";

        return $"{category.Holds} {limits}. 0 keeps the game's limit, and items with portions or more than one use always keep it.\n"
            + $"{category.HoldsChinese}{limitsChinese}。设为 0 保持游戏上限；有份数或可多次使用的物品始终保持游戏上限。";
    }
}
