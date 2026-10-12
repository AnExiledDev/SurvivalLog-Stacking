using GameCore.HotUpdate.Battle.Logic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Stacking.Core;
using UnityEngine;

namespace Stacking.Adapters;

/// <summary>
/// What a stack is, apart from its count and place: freshness, uses, quality and flags, taken before anything moves.
/// A move into another container changes the item's speed of decay, so units split off after the move are made
/// from what the stack was before it.
/// </summary>
internal sealed class StackCopy
{
    private readonly int _configId;
    private readonly Cell _stoodAt;
    private readonly Vector2Int _size;
    private readonly int _startTime;
    private readonly int _timeLeft;
    private readonly int _originalTimeLeft;
    private readonly float _timeLeftFrac;
    private readonly float _timeScale;
    private readonly int _useTimes;
    private readonly int _maxUseTimes;
    private readonly Il2CppStructArray<float>? _instanceVD;
    private readonly Il2CppStructArray<float>? _instanceEffectEnd;
    private readonly int _instanceBurnValue;
    private readonly bool _isPolluted;
    private readonly bool _isMapPreset;
    private readonly bool _hasNoPackage;

    private StackCopy(ItemData item)
    {
        _configId = item.ItemConfigId;
        _stoodAt = new Cell(item.BagPos.x, item.BagPos.y);
        _size = item.ItemSize;
        _startTime = item.StartTime;
        _timeLeft = item.TimeLeft;
        _originalTimeLeft = item.OriginalTimeLeft;
        _timeLeftFrac = item.TimeLeftFrac;
        _timeScale = item.TimeScale;
        _useTimes = item.UseTimes;
        _maxUseTimes = item.MaxUseTimes;
        _instanceVD = Clone(item.InstanceVD);
        _instanceEffectEnd = Clone(item.InstanceEffectEnd);
        _instanceBurnValue = item.InstanceBurnValue;
        _isPolluted = item.Polluted;
        _isMapPreset = item.IsMapPreset;
        _hasNoPackage = item.NoPackage;
    }

    public static StackCopy Of(ItemData item) => new(item);

    public int ConfigId => _configId;

    /// <summary>The cell the stack's top-left corner stood on when it was taken.</summary>
    public Cell StoodAt => _stoodAt;

    public Vector2Int Size => _size;

    /// <summary>
    /// A new stack of these units, never with a weight of its own. It starts its own stack at the cell it is given
    /// while NewStacks is in; without it the game may add the units to a matching stack in the same bag.
    /// </summary>
    public ItemData? AddNewStack(ItemManager items, long ownerId, Cell at, int units) =>
        NewStacks.Start(() => items.AddItem(ownerId, _configId, units, _startTime, _timeLeft, new Vector2Int(at.X, at.Y), _useTimes,
            skipBagPos: false, timeScale: _timeScale, instanceVD: Clone(_instanceVD), instanceEffectEnd: Clone(_instanceEffectEnd),
            maxUseTimes: _maxUseTimes, instanceBurnValue: _instanceBurnValue, polluted: _isPolluted, isMapPreset: _isMapPreset,
            noPackage: _hasNoPackage, instanceWeight: 0));

    /// <summary>
    /// The game sets a new stack's speed of decay from the bag it lands in. In a bag that keeps the same speed the
    /// fractions the add leaves out are copied too, so the two parts age as one.
    /// </summary>
    public void CopyFreshnessTo(ItemData created)
    {
        if (created.TimeScale != _timeScale) return;

        created.OriginalTimeLeft = _originalTimeLeft;
        created.TimeLeftFrac = _timeLeftFrac;
    }

    /// <summary>Each stack gets its own copy, or a change to one stack's values would show on the other.</summary>
    private static Il2CppStructArray<float>? Clone(Il2CppStructArray<float>? values)
    {
        if (values == null) return null;

        var copy = new Il2CppStructArray<float>(values.Length);

        for (var i = 0; i < values.Length; i++) copy[i] = values[i];

        return copy;
    }
}
