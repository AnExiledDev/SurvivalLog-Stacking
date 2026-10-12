using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Stacking.Core;
using ItemPredicate = Il2CppSystem.Func<GameCore.HotUpdate.Battle.Logic.ItemData, bool>;

namespace Stacking.Adapters;

/// <summary>
/// Keeps items the game never stacked from joining a stack of different freshness or uses. The game's stack search
/// only compares quality values, so without this a fresh meal would merge into a rotting one and take its age.
/// The search is narrowed to matching stacks; if that cannot be done, a mismatched find is refused and the item
/// starts a new stack, which is what the game did before the limit was raised. Static because Harmony patches are.
/// </summary>
internal static class StackGuard
{
    private const float SecondsPerHour = 3600f;
    private const int MaxLoggedFailures = 3;
    private const int MaxLoggedDecisions = 10;

    /// <summary>Set by the limit writer on each sweep.</summary>
    internal static IReadOnlySet<int> NewlyStackable = new HashSet<int>();

    /// <summary>Seeds that never spoil, which join a stack whatever their freshness; set by the limit writer on each sweep.</summary>
    internal static IReadOnlySet<int> NeverSpoils = new HashSet<int>();

    /// <summary>The item being added, for adds that give the game its values instead of an item to copy.</summary>
    [ThreadStatic] private static Freshness? _adding;

    private static Action<string> _log = _ => { };
    private static Action<string> _logFileOnly = _ => { };
    private static bool _canNarrowSearch = true;
    private static int _failures;
    private static int _decisionsLogged;

    /// <summary>True when the stack search is guarded; without it no new item may become stackable.</summary>
    public static bool Apply(Harmony harmony, Action<string> log, Action<string> logFileOnly)
    {
        _log = log;
        _logFileOnly = logFileOnly;

        var isGuarded = TryPatch(harmony, "the stack search", () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.FindStackableItem)),
            nameof(OnlyMatchingFreshness), nameof(RefuseOtherFreshness));

        if (!isGuarded)
        {
            log("could not guard the stack search, so items the game never stacked keep a limit of 1");

            return false;
        }

        // Without these an add's freshness is unknown, and unknown always starts a new stack: safe, just less stacking.
        TryPatch(harmony, "adding to a bag", () => AccessTools.Method(typeof(ItemManager), nameof(ItemManager.TryAddItemToBag)), nameof(RememberAdd), nameof(ForgetAdd));
        TryPatch(harmony, "adding an item", () => AddItemOverload(hasUses: true), nameof(RememberAdd), nameof(ForgetAdd));
        TryPatch(harmony, "adding a plain item", () => AddItemOverload(hasUses: false), nameof(RememberPlainAdd), nameof(ForgetAdd));
        log("items that are newly stackable only join stacks of the same freshness and uses");

        return true;
    }

    private static bool TryPatch(Harmony harmony, string what, Func<MethodBase?> target, string prefix, string postfix) =>
        Hooks.TryPatch(harmony, typeof(StackGuard), what, target, _log, prefix, postfix);

    private static MethodInfo AddItemOverload(bool hasUses) =>
        AccessTools.GetDeclaredMethods(typeof(ItemManager))
            .Single(method => method.Name == nameof(ItemManager.AddItem) && method.GetParameters().Any(p => p.Name == "useTimes") == hasUses);

    private static void RememberAdd(int startTime, int timeLeft, int useTimes, float timeScale, int maxUseTimes, out Freshness? __state)
    {
        __state = _adding;
        _adding = new Freshness(startTime, timeLeft, 0f, timeScale, useTimes, maxUseTimes);
    }

    private static void RememberPlainAdd(int startTime, int timeLeft, out Freshness? __state)
    {
        __state = _adding;
        _adding = new Freshness(startTime, timeLeft, 0f, 1f, 0, 0);
    }

    private static void ForgetAdd(Freshness? __state) => _adding = __state;

    private static void OnlyMatchingFreshness(int itemConfigId, ref ItemPredicate? extraPredicate)
    {
        if (!_canNarrowSearch || !NewlyStackable.Contains(itemConfigId)) return;

        try
        {
            var gameCheck = extraPredicate;
            var incoming = Incoming(gameCheck);
            var now = NowHours();

            extraPredicate = DelegateSupport.ConvertDelegate<ItemPredicate>(
                new Func<ItemData, bool>(stack => CanJoin(itemConfigId, gameCheck, incoming, now, stack)));
        }
        catch (Exception ex)
        {
            _canNarrowSearch = false;
            _log("could not narrow the stack search, mismatched stacks are refused instead: " + ex.Message);
        }
    }

    /// <summary>Only does anything once narrowing the search has failed; until then every find already matches.</summary>
    private static void RefuseOtherFreshness(int itemConfigId, ItemPredicate? extraPredicate, ref ItemData? __result)
    {
        if (_canNarrowSearch || __result == null || !NewlyStackable.Contains(itemConfigId)) return;

        try
        {
            if (!Matches(itemConfigId, Incoming(extraPredicate), NowHours(), __result)) __result = null;
        }
        catch (Exception ex)
        {
            Fail("refusing a stack", ex);
            __result = null;
        }
    }

    /// <summary>Called by the game for each candidate stack; an escaping exception would reach native code.</summary>
    private static bool CanJoin(int itemConfigId, ItemPredicate? gameCheck, Freshness? incoming, float? now, ItemData stack)
    {
        try
        {
            return (gameCheck == null || gameCheck.Invoke(stack)) && Matches(itemConfigId, incoming, now, stack);
        }
        catch (Exception ex)
        {
            Fail("checking a stack", ex);

            return false;
        }
    }

    private static bool Matches(int itemConfigId, Freshness? incoming, float? now, ItemData stack)
    {
        var isMatch = StackIdentity.CanJoin(incoming, Read(stack), now, NeverSpoils.Contains(itemConfigId));

        if (_decisionsLogged < MaxLoggedDecisions)
        {
            _decisionsLogged++;
            _logFileOnly($"item {itemConfigId}: {(isMatch ? "joins" : "does not join")} a stack (incoming {Describe(incoming)}, stack {Describe(Read(stack))}, now {now?.ToString("0.0") ?? "unknown"}h)");
        }

        return isMatch;
    }

    /// <summary>
    /// Moving an item hands the game a check that closes over the item itself (moving a whole bag, or one item); any
    /// other add was remembered from its arguments.
    /// </summary>
    private static Freshness? Incoming(ItemPredicate? gameCheck)
    {
        var closure = gameCheck?.Target;
        var moving = closure?.TryCast<ItemManager.__c__DisplayClass100_0>()?.item ?? closure?.TryCast<ItemManager.__c__DisplayClass93_0>()?.item;

        return moving != null ? Read(moving) : _adding;
    }

    internal static float? NowHours()
    {
        if (!BaseSingleton<BattleLogicWorld>.IsInstanceCreated) return null;

        var time = BaseSingleton<BattleLogicWorld>.Instance._GameTimeManager;

        return time == null ? null : time.GetTotalSeconds() / SecondsPerHour;
    }

    internal static Freshness Read(ItemData item) =>
        new(item.StartTime, item.TimeLeft, item.TimeLeftFrac, item.TimeScale, item.UseTimes, item.MaxUseTimes);

    private static string Describe(Freshness? item) =>
        item is { } f ? $"expires {f.ExpiresAt:0.0}h x{f.TimeScale:0.##}, uses {f.UseTimes}/{f.MaxUseTimes}" : "unknown";

    private static void Fail(string what, Exception ex)
    {
        _failures++;

        if (_failures <= MaxLoggedFailures) _log($"{what} failed, the item starts a new stack (logged {_failures} of {MaxLoggedFailures}): {ex}");
    }
}
