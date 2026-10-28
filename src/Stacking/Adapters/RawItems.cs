using System;
using System.Collections.Generic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime;
using Il2CppHeld = Il2CppSystem.Collections.Generic.Dictionary<long, GameCore.HotUpdate.ReduxUI.Data_Item>;
using OwnerCache = Il2CppSystem.Collections.Generic.Dictionary<long, Il2CppSystem.Collections.Generic.Dictionary<long, GameCore.HotUpdate.ReduxUI.Data_Item>>;

namespace Stacking.Adapters;

/// <summary>
/// The UI store's item cache read straight from the game's memory, so reading a container makes no interop object per
/// item. The fields' places come from the game's own type data, once; every item's class is checked before it is read.
/// Anything not laid out as expected turns this reader off for good, says so once, and the caller goes through the
/// game's objects instead. What a read finds is only good for the frame it was taken in. Main thread only.
/// </summary>
internal sealed unsafe class RawItems
{
    /// <summary>One item as the cache holds it.</summary>
    public readonly record struct Item(long LogicId, long OwnerId, int Count, int ConfigId, bool HasOwnWeight);

    private readonly Func<Action<string>> _log;
    private readonly Dictionary<long, IntPtr> _heldByOwner = new();

    private bool _hasLearned;
    private bool _isDirect;
    private RawDictionary? _owners;
    private RawDictionary? _held;
    private IntPtr _itemClass;
    private int _logicIdAt;
    private int _ownerIdAt;
    private int _itemCountAt;
    private int _itemConfigIdAt;
    private int _instanceWeightAt;

    /// <param name="log">The log as it is when a line is written.</param>
    public RawItems(Func<Action<string>> log)
    {
        _log = log;
    }

    /// <summary>One pass over the cache's owners; false when this reader is off or the cache is not as expected.</summary>
    public bool TryOpen(OwnerCache cache)
    {
        _heldByOwner.Clear();
        Learn(cache);

        if (!_isDirect) return false;

        if (_owners == null || !_owners.TryOpen(cache.Pointer, out var entries, out var used)) return GiveUp("the item cache");

        for (var i = 0; i < used; i++)
        {
            if (!RawDictionary.IsLive(entries, i)) continue;

            var held = RawDictionary.Value(entries, i);

            if (held != IntPtr.Zero) _heldByOwner[RawDictionary.Key(entries, i)] = held;
        }

        return true;
    }

    /// <summary>
    /// Adds every item the cache files under this owner, ghosts whose owner moved on included (the caller drops those);
    /// an owner the cache has no list for adds nothing. False, with this reader off for good, when anything is not as
    /// expected; what it added by then is left for the caller to clear.
    /// </summary>
    public bool TryAddHeld(long ownerId, List<Item> into)
    {
        if (!_isDirect) return false;

        if (!_heldByOwner.TryGetValue(ownerId, out var held)) return true;

        if (_held == null || !_held.TryOpen(held, out var entries, out var used)) return GiveUp("a container's item list");

        for (var i = 0; i < used; i++)
        {
            if (!RawDictionary.IsLive(entries, i)) continue;

            var item = RawDictionary.Value(entries, i);

            if (item == IntPtr.Zero) continue;

            if (*(IntPtr*)item != _itemClass) return GiveUp("an item");

            into.Add(new Item(
                *(long*)(item + _logicIdAt),
                *(long*)(item + _ownerIdAt),
                *(int*)(item + _itemCountAt),
                *(int*)(item + _itemConfigIdAt),
                *(int*)(item + _instanceWeightAt) != 0));
        }

        return true;
    }

    private void Learn(OwnerCache cache)
    {
        if (_hasLearned) return;

        _hasLearned = true;

        try
        {
            _owners = RawDictionary.Learn(IL2CPP.il2cpp_object_get_class(cache.Pointer));
            _held = RawDictionary.Learn(Il2CppClassPointerStore<Il2CppHeld>.NativeClassPtr);
            _itemClass = Il2CppClassPointerStore<Data_Item>.NativeClassPtr;
            _logicIdAt = RawDictionary.FieldOffset(_itemClass, "<LogicId>k__BackingField");
            _ownerIdAt = RawDictionary.FieldOffset(_itemClass, "<OwnerId>k__BackingField");
            _itemCountAt = RawDictionary.FieldOffset(_itemClass, "<ItemCount>k__BackingField");
            _itemConfigIdAt = RawDictionary.FieldOffset(_itemClass, "<ItemConfigId>k__BackingField");
            _instanceWeightAt = RawDictionary.FieldOffset(_itemClass, "<InstanceWeight>k__BackingField");
            _isDirect = _owners != null && _held != null && _itemClass != IntPtr.Zero
                && _logicIdAt > 0 && _ownerIdAt > 0 && _itemCountAt > 0 && _itemConfigIdAt > 0 && _instanceWeightAt > 0;
        }
        catch (Exception ex)
        {
            _log()("could not learn the item cache's layout, the crafting table reads its storages through the game's objects instead (reported once): " + ex.Message);

            return;
        }

        if (!_isDirect) _log()("the item cache is not laid out as expected, the crafting table reads its storages through the game's objects instead (reported once)");
    }

    /// <summary>Turns the memory reader off for good and says why, once. Always false.</summary>
    private bool GiveUp(string what)
    {
        _isDirect = false;
        _heldByOwner.Clear();
        _log()("could not read " + what + " directly, the crafting table reads its storages through the game's objects instead (reported once)");

        return false;
    }
}
