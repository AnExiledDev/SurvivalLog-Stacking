using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace Stacking.Adapters;

/// <summary>
/// Which containers are cooking or crafting stations, by the game's own tests: its cooking furniture (benches, pots,
/// juicers and the rest of the cooking table), hot pots, brew barrels, the compressor and shredder trays, and the
/// bench of the crafting table open on screen. Compost bins, racks and robot molds are not stations; they keep their
/// own rules.
/// </summary>
internal static class Stations
{
    public static bool IsStation(ItemManager items, long ownerId) =>
        ownerId != 0
        && (ItemManager.IsCookingFurniture(ownerId)
            || items.IsCookingFurnitureBag(ownerId)
            || ItemManager.IsHotPotWorkbench(ownerId)
            || Furniture.IsBrewBarrelOwner(ownerId)
            || CompressorConfig.IsTrayOwner(ownerId)
            || ShredderConfig.IsTrayOwner(ownerId)
            || IsOpenCraftingBench(ownerId));

    /// <summary>The crafting table has no test of its own; its bench is known only while its screen is open, which is the only time items go in.</summary>
    private static bool IsOpenCraftingBench(long ownerId)
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = Il2CppType.Of<WebUI_ToolTable>();

        if (ui == null || !ui.IsUIActive(screen)) return false;

        return ui.GetStateByUI(screen)?.TryCast<State_Web_ToolTable>()?.WorkbenchOwnerId?.Value == ownerId;
    }
}
