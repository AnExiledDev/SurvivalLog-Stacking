using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;

namespace Stacking.Adapters;

/// <summary>The bag screen as the game draws it: which containers it shows and how big it draws their grids.</summary>
internal static class BackpackScreen
{
    /// <summary>The bag screen's state while it is open; null when it is closed.</summary>
    public static State_Web_BackpackUI? Open()
    {
        var ui = GameSingleton.Existing<ReduxUISystem>();
        var screen = Il2CppType.Of<WebUI_BackpackUI>();

        if (ui == null || !ui.IsUIActive(screen)) return null;

        return ui.GetStateByUI(screen)?.TryCast<State_Web_BackpackUI>();
    }

    /// <summary>The grid the open bag screen draws for a container; null when the screen does not show it.</summary>
    public static (int Columns, int Rows)? DrawnGrid(long ownerId)
    {
        if (Open() is not { } bag) return null;

        if (bag.OwnerId_A == ownerId) return (bag.BagCols_A, bag.BagRows_A);

        if (bag.IsDualBag && bag.OwnerId_B == ownerId) return (bag.BagCols_B, bag.BagRows_B);

        return null;
    }
}
