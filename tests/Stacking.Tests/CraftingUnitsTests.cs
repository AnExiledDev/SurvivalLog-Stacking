using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>
/// The crafting fill takes a recipe's units onto the bench one at a time, each off the stack the last one left
/// behind. It is all or nothing: a fill that stops part way sends back every unit it took, and no unit is ever made
/// or lost.
/// </summary>
public class CraftingUnitsTests
{
    private const long Bag = 1;
    private const long Bench = 50;
    private const long Nail = 7;

    private static readonly Cell NailAt = new(2, 1);

    private static FakeStationStore Workshop(int nails) => new FakeStationStore().Station(Bench).Item(Nail, Bag, NailAt, nails);

    private static StationReport TakeOne(FakeStationStore store, long stackId, long ownerId) =>
        StationMoves.TakeOne(store, stackId, ownerId, Bench, store.MovesInto(stackId, Bench));

    private static Func<long, long, bool> MovesBack(FakeStationStore store) => (unitId, toOwnerId) =>
    {
        store.MoveTo(unitId, toOwnerId, new Cell(4, 4));

        return true;
    };

    private static UnitsTaken Take(FakeStationStore store, int units, Func<long, long, StationReport> takeOne, Func<long, long, bool> moveBack) =>
        CraftingUnits.Take(store.Find, [new UnitTake(Nail, Bag, units)], Bench, takeOne, moveBack);

    /// <summary>The second unit is the stack's last, so it stays a whole pick for the game's own move rather than a cut.</summary>
    [Fact]
    public void Two_units_off_a_stack_of_two_put_one_on_the_bench_and_leave_the_last_for_the_game_to_move()
    {
        var store = Workshop(2);

        var result = Take(store, 2, (stackId, ownerId) => TakeOne(store, stackId, ownerId), MovesBack(store));

        var landed = Assert.IsType<UnitsTaken.Landed>(result);
        var (restId, rest) = Assert.Single(store.In(Bag));

        Assert.Equal(new[] { new BenchUnit(Nail, Bag) }, landed.OnBench);
        Assert.Equal(new[] { new WholePick(restId, Bag) }, landed.Whole);
        Assert.Equal(1, rest.Count);
        Assert.Equal(new Placed(Bench, new Cell(0, 0), 1), store.At(Nail));
        Assert.Equal(2, store.TotalUnits);
    }

    [Fact]
    public void Two_units_off_a_stack_of_eight_put_two_single_units_on_the_bench_and_leave_six_in_the_bag()
    {
        var store = Workshop(8);

        var result = Take(store, 2, (stackId, ownerId) => TakeOne(store, stackId, ownerId), MovesBack(store));

        var landed = Assert.IsType<UnitsTaken.Landed>(result);
        var (_, rest) = Assert.Single(store.In(Bag));

        Assert.Equal(2, landed.OnBench.Count);
        Assert.Equal(new BenchUnit(Nail, Bag), landed.OnBench[0]);
        Assert.NotEqual(Nail, landed.OnBench[1].UnitId);
        Assert.All(landed.OnBench, unit => Assert.Equal(new Placed(Bench, new Cell(0, 0), 1), store.At(unit.UnitId)));
        Assert.Empty(landed.Whole);
        Assert.Equal(6, rest.Count);
        Assert.Equal(8, store.TotalUnits);
    }

    [Fact]
    public void A_unit_the_bench_refuses_sends_the_units_already_taken_back_and_keeps_every_unit()
    {
        var store = Workshop(8);
        var takes = 0;

        var result = Take(
            store,
            2,
            (stackId, ownerId) => StationMoves.TakeOne(store, stackId, ownerId, Bench, ++takes == 1 ? store.MovesInto(stackId, Bench) : store.Refuses()),
            MovesBack(store));

        var putBack = Assert.IsType<UnitsTaken.PutBack>(result);

        Assert.Empty(putBack.StillOnBench);
        Assert.NotEmpty(putBack.Notes);
        Assert.Empty(store.InStations);
        Assert.Equal(Bag, store.At(Nail)?.OwnerId);
        Assert.Equal(8, store.TotalUnits);
    }

    /// <summary>A unit the game would not move back is a real item left on the bench, so the report names it.</summary>
    [Fact]
    public void A_unit_the_game_will_not_move_back_is_named_as_still_on_the_bench()
    {
        var store = Workshop(8);
        var takes = 0;

        var result = Take(
            store,
            2,
            (stackId, ownerId) => StationMoves.TakeOne(store, stackId, ownerId, Bench, ++takes == 1 ? store.MovesInto(stackId, Bench) : store.Refuses()),
            (_, _) => false);

        var putBack = Assert.IsType<UnitsTaken.PutBack>(result);

        Assert.Equal(new[] { new BenchUnit(Nail, Bag) }, putBack.StillOnBench);
        Assert.Equal(Bench, store.At(Nail)?.OwnerId);
        Assert.Equal(8, store.TotalUnits);
    }
}
