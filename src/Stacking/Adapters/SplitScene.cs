using System.Collections.Generic;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using Stacking.Core;

namespace Stacking.Adapters;

/// <summary>A drop the player asked to split: which stack, from which bag to which, the cell it landed on and the cell under the cursor.</summary>
internal sealed record SplitRequest(long ItemId, long SrcOwnerId, long DstOwnerId, SplitKind Kind, Cell DropAt, Cell? Cursor);

/// <summary>Reads the live bags into the core's terms and asks it what a split drop should do.</summary>
internal static class SplitScene
{
    public static ItemManager? LiveItems() => GameSingleton.Existing<BattleLogicWorld>()?._ItemManager;

    /// <summary>The game can be left with a second item manager answering events for stale data; only the live one counts.</summary>
    public static bool IsLive(ItemManager items) => LiveItems() is { } live && live.Pointer == items.Pointer;

    /// <summary>Vanilla when the stack is no longer the one dragged, or carries its own weight, which the game never stacks.</summary>
    public static SplitPlan Plan(ItemManager items, ItemData source, SplitRequest request, int? chosenUnits)
    {
        if (source.OwnerId != request.SrcOwnerId || source.InstanceWeight != 0) return new SplitPlan.Vanilla();

        var config = GameSingleton.Existing<ConfigManager>()?.Get_Config_Item(source.ItemConfigId);

        if (config == null) return new SplitPlan.Vanilla();

        var size = source.ItemSize;
        var drag = new StackDrag(source.InstanceId, source.ItemCount, size.x, size.y, request.DropAt, request.Cursor);

        return Splitting.Plan(request.Kind, drag, Grid(items, source, request), config.StackLimit, UnitsWeightAllows(items, request, config.weight), chosenUnits);
    }

    private static BagGrid Grid(ItemManager items, ItemData source, SplitRequest request)
    {
        var size = items.GetOwnerBagSize(request.DstOwnerId);
        var inBag = items.GetItemDataList(request.DstOwnerId);
        var dragged = Facts(source);
        var isNewlyStackable = StackGuard.NewlyStackable.Contains(source.ItemConfigId);
        var nowHours = StackGuard.NowHours();
        var stacks = new List<GridItem>();

        for (var i = 0; inBag != null && i < inBag.Count; i++)
        {
            var item = inBag[i];

            if (item == null) continue;

            var isSameQuality = item.ItemConfigId == source.ItemConfigId && IsSameQuality(source, item);
            var match = Splitting.MatchOf(dragged, Facts(item), isSameQuality, isNewlyStackable, nowHours);
            var cells = new Footprint(item.BagPos.x, item.BagPos.y, item.ItemSize.x, item.ItemSize.y);

            stacks.Add(new GridItem(item.InstanceId, cells, item.ItemCount, match));
        }

        var hasOwnRules = HasOwnRules(items, request.SrcOwnerId) || HasOwnRules(items, request.DstOwnerId);

        return new BagGrid(size.x, size.y, stacks, hasOwnRules);
    }

    private static StackFacts Facts(ItemData item) =>
        new(item.ItemConfigId, item.Polluted, item.IsMapPreset, item.InstanceWeight != 0, StackGuard.Read(item));

    /// <summary>The game's own merge compares quality values twice, the shown values and when their effects end; so does this.</summary>
    private static bool IsSameQuality(ItemData a, ItemData b) =>
        ItemValueDisplayHelper.AreVdEqual(a.InstanceVD, b.InstanceVD) && ItemValueDisplayHelper.AreVdEqual(a.InstanceEffectEnd, b.InstanceEffectEnd);

    /// <summary>The game's move test: a bag with a carry limit refuses a move that would end above it.</summary>
    private static int UnitsWeightAllows(ItemManager items, SplitRequest request, int unitWeight)
    {
        var hasBagConfig = items.TryGetTargetBagConfig(request.DstOwnerId, out _, out var maxWeight, out var checksBurden);

        return Splitting.UnitsWeightAllows(request.SrcOwnerId == request.DstOwnerId, hasBagConfig && checksBurden, maxWeight,
            items.GetBagCurrentWeight(request.DstOwnerId), unitWeight);
    }

    /// <summary>Benches, compost, racks, crafting slots, robot molds and hot pots run their own code on every change.</summary>
    private static bool HasOwnRules(ItemManager items, long ownerId) =>
        items.IsCookingFurnitureBag(ownerId)
        || items.IsInCompostBin(ownerId)
        || ItemManager.IsMaterialRackOwner(ownerId)
        || ItemManager.IsSyntheticBagOwner(ownerId)
        || ItemManager.GetRobotMoldCells(ownerId) is { Count: > 0 }
        || ItemManager.IsHotPotWorkbench(ownerId);
}
