using Stacking.Core;
using Xunit;

namespace Stacking.Tests;

/// <summary>How a step of the fake game goes wrong.</summary>
public enum Fault
{
    /// <summary>The call throws and did nothing.</summary>
    Throws,

    /// <summary>The call did its work and then threw, so the caller cannot trust either outcome.</summary>
    ThrowsAfterWorking,

    /// <summary>The call returns nothing and did nothing.</summary>
    ReturnsNothing,

    /// <summary>The add put the units on a stack already in the container instead of starting one.</summary>
    TopsUpAStack,

    /// <summary>The add put one unit on a stack already in the container and started a new stack with the rest.</summary>
    TopsUpAndSpills,

    /// <summary>The add started a stack, but with a unit too many.</summary>
    WrongCount,

    /// <summary>The add started the stack somewhere else, as on the ground beside the player.</summary>
    LandsElsewhere,
}

/// <summary>
/// The game's items as a plain table, where any step can be told to go wrong once or every time. Counts every unit
/// so a test can see none was made or lost, and notes any stack taken away that the steps did not add themselves.
/// </summary>
public sealed class FakeStationStore : IStationStore
{
    public const string Replay = "Replay";
    public const long Ground = 0;

    private readonly Dictionary<long, Placed> _items = new();
    private readonly HashSet<long> _stations = new();
    private readonly HashSet<long> _made = new();
    private readonly Dictionary<string, (Fault Fault, int TimesLeft)> _faults = new();
    private long _nextId = 100;
    private string? _blindAfter;
    private bool _isBlind;

    public List<long> RemovedNotMade { get; } = new();

    public int ThrowsFired { get; private set; }

    /// <summary>Reads of an item that failed, which the steps must never guess past.</summary>
    public int UnreadableReads { get; private set; }

    public Cell FreeCellAnswer { get; set; } = new(3, 3);

    public FakeStationStore Station(long ownerId)
    {
        _stations.Add(ownerId);

        return this;
    }

    public FakeStationStore Item(long itemId, long ownerId, Cell at, int count)
    {
        _items[itemId] = new Placed(ownerId, at, count);

        return this;
    }

    /// <summary>The step goes wrong this many times, then works; null times means every time.</summary>
    public FakeStationStore Failing(string step, Fault fault, int? times = null)
    {
        _faults[step] = (fault, times ?? -1);

        return this;
    }

    /// <summary>Every read of an item fails from the moment the step is first called.</summary>
    public FakeStationStore UnreadableAfter(string step)
    {
        _blindAfter = step;

        return this;
    }

    /// <summary>Every read of an item fails from the start.</summary>
    public FakeStationStore Unreadable()
    {
        _isBlind = true;

        return this;
    }

    public int TotalUnits => _items.Values.Sum(item => item.Count);

    /// <summary>Where an item really is, for the test's own checks; null when it is gone.</summary>
    public Placed? At(long itemId) => _items.TryGetValue(itemId, out var item) ? item : null;

    public IEnumerable<Placed> InStations => _items.Values.Where(item => _stations.Contains(item.OwnerId));

    public IEnumerable<(long Id, Placed Item)> In(long ownerId) => _items.Where(pair => pair.Value.OwnerId == ownerId).Select(pair => (pair.Key, pair.Value));

    public ItemLookup Find(long itemId)
    {
        if (_isBlind)
        {
            ThrowsFired++;
            UnreadableReads++;

            return new ItemLookup.Unreadable("boom in Find");
        }

        return At(itemId) is { } item ? new ItemLookup.Found(item) : new ItemLookup.Gone();
    }

    public bool IsStation(long ownerId)
    {
        ThrowIfFailing(nameof(IsStation));

        return _stations.Contains(ownerId);
    }

    public IReadOnlyDictionary<long, int> CountsIn(long ownerId)
    {
        ThrowIfFailing(nameof(CountsIn));

        return In(ownerId).ToDictionary(pair => pair.Id, pair => pair.Item.Count);
    }

    public void SetCount(long itemId, int count)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), "a stack never holds less than one unit");

        Run(nameof(SetCount), () => _items[itemId] = Existing(itemId) with { Count = count });
    }

    public void MoveTo(long itemId, long ownerId, Cell at) =>
        Run(nameof(MoveTo), () => _items[itemId] = Existing(itemId) with { OwnerId = ownerId, At = at });

    public long? AddStack(long ownerId, Cell at, int units)
    {
        switch (Next(nameof(AddStack)))
        {
            case Fault.Throws:
                ThrowsFired++;

                throw new InvalidOperationException("boom in AddStack");
            case Fault.ThrowsAfterWorking:
                Made(ownerId, at, units);
                ThrowsFired++;

                throw new InvalidOperationException("boom in AddStack after it worked");
            case Fault.ReturnsNothing:
                return null;
            case Fault.TopsUpAStack when FirstIn(ownerId) is { } existing:
                _items[existing] = _items[existing] with { Count = _items[existing].Count + units };

                return existing;
            case Fault.TopsUpAndSpills when FirstIn(ownerId) is { } existing:
                _items[existing] = _items[existing] with { Count = _items[existing].Count + 1 };

                return units > 1 ? Made(ownerId, at, units - 1) : existing;
            case Fault.WrongCount:
                return Made(ownerId, at, units + 1);
            case Fault.LandsElsewhere:
                return Made(Ground, at, units);
            default:
                return Made(ownerId, at, units);
        }
    }

    public void Remove(long itemId) =>
        Run(nameof(Remove), () =>
        {
            if (!_made.Contains(itemId)) RemovedNotMade.Add(itemId);

            _items.Remove(itemId);
        });

    public Cell? FreeCell(long ownerId, long itemId, Cell near)
    {
        if (Next(nameof(FreeCell)) is { } fault)
        {
            if (fault == Fault.ReturnsNothing) return null;

            ThrowsFired++;

            throw new InvalidOperationException("boom in FreeCell");
        }

        return FreeCellAnswer;
    }

    /// <summary>The game's own move of the unit into the station, as the steps replay it.</summary>
    public Action MovesInto(long itemId, long stationOwnerId) =>
        () => Run(Replay, () => _items[itemId] = Existing(itemId) with { OwnerId = stationOwnerId, At = new Cell(0, 0) });

    /// <summary>The game's move, refused: nothing happens, as when the station is full.</summary>
    public Action Refuses() => () => Run(Replay, () => { });

    private long? FirstIn(long ownerId) => In(ownerId).Select(pair => (long?)pair.Id).FirstOrDefault();

    private long Made(long ownerId, Cell at, int units)
    {
        var id = _nextId++;

        _items[id] = new Placed(ownerId, at, units);
        _made.Add(id);

        return id;
    }

    private Placed Existing(long itemId) =>
        _items.TryGetValue(itemId, out var item) ? item : throw new InvalidOperationException($"no item {itemId}");

    private void Run(string step, Action work)
    {
        var fault = Next(step);

        if (fault == Fault.Throws)
        {
            ThrowsFired++;

            throw new InvalidOperationException($"boom in {step}");
        }

        work();

        if (fault != Fault.ThrowsAfterWorking) return;

        ThrowsFired++;

        throw new InvalidOperationException($"boom in {step} after it worked");
    }

    private void ThrowIfFailing(string step)
    {
        if (Next(step) == null) return;

        ThrowsFired++;

        throw new InvalidOperationException($"boom in {step}");
    }

    private Fault? Next(string step)
    {
        if (step == _blindAfter) _isBlind = true;

        if (!_faults.TryGetValue(step, out var failing) || failing.TimesLeft == 0) return null;

        if (failing.TimesLeft > 0) _faults[step] = failing with { TimesLeft = failing.TimesLeft - 1 };

        return failing.Fault;
    }
}

/// <summary>
/// The station rule: a cooking or crafting station never holds a stack of an item this mod made stackable, and no
/// unit is ever made or lost on the way, whichever of the game's calls goes wrong.
/// </summary>
public class StationMovesTests
{
    private const long Bag = 1;
    private const long Pot = 50;
    private const long Egg = 7;
    private const long PlayersStack = 8;
    private const int Eggs = 5;
    private const int PlayersEggs = 3;
    private const int AllUnits = Eggs + PlayersEggs;

    private static readonly Cell EggAt = new(2, 1);

    /// <summary>Five eggs in the bag, another stack of three beside them, and an empty pot.</summary>
    private static FakeStationStore Kitchen() => new FakeStationStore().Station(Pot).Item(Egg, Bag, EggAt, Eggs).Item(PlayersStack, Bag, new Cell(0, 0), PlayersEggs);

    /// <summary>A whole-bag move left the five eggs in the pot.</summary>
    private static FakeStationStore EggsLeftInThePot() => new FakeStationStore().Station(Pot).Item(Egg, Pot, new Cell(0, 0), Eggs).Item(PlayersStack, Bag, new Cell(0, 0), PlayersEggs);

    private static StationReport TakeOne(FakeStationStore store) => StationMoves.TakeOne(store, Egg, Bag, Pot, store.MovesInto(Egg, Pot));

    private static StationReport SendBack(FakeStationStore store) => StationMoves.SendBack(store, Egg, Pot, Bag, EggAt);

    public static TheoryData<string, Fault, int?> EveryStepGoingWrong()
    {
        var data = new TheoryData<string, Fault, int?>();
        var faults = new Dictionary<string, Fault[]>
        {
            [nameof(IStationStore.IsStation)] = [Fault.Throws],
            [nameof(IStationStore.CountsIn)] = [Fault.Throws],
            [nameof(IStationStore.SetCount)] = [Fault.Throws, Fault.ThrowsAfterWorking],
            [nameof(IStationStore.MoveTo)] = [Fault.Throws, Fault.ThrowsAfterWorking],
            [nameof(IStationStore.AddStack)] = [Fault.Throws, Fault.ThrowsAfterWorking, Fault.ReturnsNothing, Fault.TopsUpAStack, Fault.TopsUpAndSpills, Fault.WrongCount, Fault.LandsElsewhere],
            [nameof(IStationStore.Remove)] = [Fault.Throws, Fault.ThrowsAfterWorking],
            [nameof(IStationStore.FreeCell)] = [Fault.Throws, Fault.ReturnsNothing],
            [FakeStationStore.Replay] = [Fault.Throws, Fault.ThrowsAfterWorking],
        };

        foreach (var (step, kinds) in faults)
        {
            foreach (var fault in kinds)
            {
                data.Add(step, fault, 1);
                data.Add(step, fault, null);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStepGoingWrong))]
    public void Moving_a_stack_into_a_station_keeps_every_unit_and_never_leaves_a_stack_there(string step, Fault fault, int? times)
    {
        var store = Kitchen().Failing(step, fault, times);

        var report = TakeOne(store);

        AssertRuleHeld(store, report);
    }

    [Theory]
    [MemberData(nameof(EveryStepGoingWrong))]
    public void Sending_a_stack_back_out_of_a_station_keeps_every_unit_and_never_leaves_a_stack_there(string step, Fault fault, int? times)
    {
        var store = EggsLeftInThePot().Failing(step, fault, times);

        var report = SendBack(store);

        AssertRuleHeld(store, report);
    }

    [Fact]
    public void One_egg_goes_into_the_pot_and_the_rest_stay_where_the_stack_stood()
    {
        var store = Kitchen();

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.OneUnitIn, report.Outcome);
        Assert.Empty(report.Notes);
        Assert.Equal(new Placed(Pot, new Cell(0, 0), 1), store.At(Egg));
        Assert.Contains(store.In(Bag), pair => pair.Item == new Placed(Bag, EggAt, Eggs - 1));
    }

    [Fact]
    public void A_pot_that_takes_nothing_leaves_the_stack_whole_where_it_was()
    {
        var store = Kitchen();

        var report = StationMoves.TakeOne(store, Egg, Bag, Pot, store.Refuses());

        Assert.Equal(StationOutcome.NothingMoved, report.Outcome);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Empty(store.InStations);
    }

    /// <summary>The screen changed between the stopped move and the replay, so the container is no longer a station: nothing is touched.</summary>
    [Fact]
    public void A_target_that_is_no_longer_a_station_is_left_alone()
    {
        var store = new FakeStationStore().Item(Egg, Bag, EggAt, Eggs);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.Skipped, report.Outcome);
        Assert.Empty(report.Notes);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
    }

    [Fact]
    public void A_stack_that_changed_before_the_move_is_left_alone()
    {
        var store = Kitchen().Item(Egg, Bag, EggAt, 1);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.Skipped, report.Outcome);
        Assert.Equal(new Placed(Bag, EggAt, 1), store.At(Egg));
    }

    /// <summary>The rest could not start a stack of its own, and the rule forbids it going in with the unit, so the move is undone.</summary>
    [Fact]
    public void When_the_rest_cannot_be_kept_apart_the_unit_comes_back_and_the_stack_is_whole()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.ReturnsNothing);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.MoveUndone, report.Outcome);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Empty(store.InStations);
    }

    [Fact]
    public void When_the_unit_cannot_come_back_either_the_rest_goes_anywhere_in_the_bag()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.ReturnsNothing, times: 1).Failing(nameof(IStationStore.MoveTo), Fault.Throws);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.RestElsewhere, report.Outcome);
        Assert.Equal(new Placed(Pot, new Cell(0, 0), 1), store.At(Egg));
        Assert.Contains(store.In(Bag), pair => pair.Item == new Placed(Bag, store.FreeCellAnswer, Eggs - 1));
        Assert.Equal(AllUnits, store.TotalUnits);
    }

    /// <summary>Only when the bag takes the rest nowhere are units lost, and then the report says how many, loudly.</summary>
    [Fact]
    public void Units_the_game_will_take_nowhere_are_reported_and_never_put_in_the_station()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.ReturnsNothing).Failing(nameof(IStationStore.MoveTo), Fault.Throws);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.CountsWrong, report.Outcome);
        Assert.True(report.NeedsAttention);
        Assert.Contains(report.Notes, note => note.Contains($"{Eggs - 1} of {Eggs} units"));
        Assert.All(store.InStations, item => Assert.Equal(1, item.Count));
    }

    [Fact]
    public void A_stack_the_game_topped_up_instead_of_starting_one_gets_its_own_count_back_and_is_never_taken_away()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.TopsUpAStack);

        var report = TakeOne(store);

        Assert.Equal(PlayersEggs, store.At(PlayersStack)?.Count);
        Assert.Empty(store.RemovedNotMade);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Contains(report.Notes, note => note.Contains("already there"));
    }

    [Fact]
    public void Every_failure_caught_is_reported_with_what_the_game_threw()
    {
        var store = Kitchen().Failing(FakeStationStore.Replay, Fault.Throws);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.NothingMoved, report.Outcome);
        Assert.True(report.NeedsAttention);
        Assert.Contains(report.Notes, note => note.Contains("boom in Replay"));
    }

    [Fact]
    public void A_stack_found_in_a_station_keeps_one_unit_there_and_sends_the_rest_back()
    {
        var store = EggsLeftInThePot();

        var report = SendBack(store);

        Assert.Equal(StationOutcome.SentBack, report.Outcome);
        Assert.Empty(report.Notes);
        Assert.Equal(1, store.At(Egg)?.Count);
        Assert.Contains(store.In(Bag), pair => pair.Item == new Placed(Bag, store.FreeCellAnswer, Eggs - 1));
    }

    [Fact]
    public void A_stack_that_cannot_be_cut_goes_back_whole()
    {
        var store = EggsLeftInThePot().Failing(nameof(IStationStore.AddStack), Fault.Throws);

        var report = SendBack(store);

        Assert.Equal(StationOutcome.StackMovedBack, report.Outcome);
        Assert.Equal(new Placed(Bag, store.FreeCellAnswer, Eggs), store.At(Egg));
    }

    [Fact]
    public void A_stack_that_can_neither_be_cut_nor_moved_stays_and_is_reported()
    {
        var store = EggsLeftInThePot().Failing(nameof(IStationStore.AddStack), Fault.Throws).Failing(nameof(IStationStore.MoveTo), Fault.Throws);

        var report = SendBack(store);

        Assert.Equal(StationOutcome.StackStays, report.Outcome);
        Assert.True(report.NeedsAttention);
        Assert.Equal(AllUnits, store.TotalUnits);
    }

    [Fact]
    public void Units_sent_back_that_cannot_be_taken_back_are_reported_as_counted_twice()
    {
        var store = EggsLeftInThePot().Failing(nameof(IStationStore.SetCount), Fault.Throws).Failing(nameof(IStationStore.Remove), Fault.Throws);

        var report = SendBack(store);

        Assert.Equal(StationOutcome.CountsWrong, report.Outcome);
        Assert.Contains(report.Notes, note => note.Contains("counted twice"));
    }

    [Fact]
    public void A_stack_that_came_from_another_station_has_nowhere_to_go_and_is_reported()
    {
        var store = EggsLeftInThePot().Station(Bag);

        var report = SendBack(store);

        Assert.Equal(StationOutcome.StackStays, report.Outcome);
        Assert.True(report.NeedsAttention);
        Assert.Equal(new Placed(Pot, new Cell(0, 0), Eggs), store.At(Egg));
    }

    /// <summary>The add put the rest in its own stack and then threw: the stack is kept, and the units are never added a second time.</summary>
    [Fact]
    public void An_add_that_worked_and_then_threw_keeps_its_stack_and_adds_nothing_twice()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.ThrowsAfterWorking);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.OneUnitIn, report.Outcome);
        Assert.Equal(AllUnits, store.TotalUnits);
        Assert.Equal(new Placed(Pot, new Cell(0, 0), 1), store.At(Egg));
        Assert.Contains(store.In(Bag), pair => pair.Item == new Placed(Bag, EggAt, Eggs - 1));
        Assert.Contains(report.Notes, note => note.Contains("boom in AddStack after it worked"));
    }

    [Fact]
    public void Units_sent_back_by_an_add_that_worked_and_then_threw_are_not_sent_twice()
    {
        var store = EggsLeftInThePot().Failing(nameof(IStationStore.AddStack), Fault.ThrowsAfterWorking);

        var report = SendBack(store);

        Assert.Equal(StationOutcome.SentBack, report.Outcome);
        Assert.Equal(AllUnits, store.TotalUnits);
        Assert.Equal(1, store.At(Egg)?.Count);
        Assert.Contains(store.In(Bag), pair => pair.Item == new Placed(Bag, store.FreeCellAnswer, Eggs - 1));
    }

    /// <summary>The game topped up the player's stack by one and started a stack of the rest: both are put right, and the move is undone.</summary>
    [Fact]
    public void An_add_that_tops_up_a_stack_and_spills_the_rest_is_undone_whole()
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.TopsUpAndSpills);

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.MoveUndone, report.Outcome);
        Assert.Equal(AllUnits, store.TotalUnits);
        Assert.Equal(PlayersEggs, store.At(PlayersStack)?.Count);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Empty(store.RemovedNotMade);
        Assert.Contains(report.Notes, note => note.Contains("1 new stacks, 1 stacks already there grew"));
    }

    [Fact]
    public void An_item_that_cannot_be_read_at_the_start_is_left_alone_and_the_game_s_move_does_not_run()
    {
        var store = Kitchen().Unreadable();

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.Unreadable, report.Outcome);
        Assert.True(report.NeedsAttention);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Equal(AllUnits, store.TotalUnits);
    }

    /// <summary>
    /// The cut to one unit failed and the stack can no longer be read. Counting it as one unit would add the other
    /// four a second time; the steps stop and report instead.
    /// </summary>
    [Fact]
    public void An_item_that_cannot_be_read_after_a_change_is_reported_and_no_units_are_added_for_it()
    {
        var store = Kitchen().Failing(nameof(IStationStore.SetCount), Fault.Throws).UnreadableAfter(nameof(IStationStore.SetCount));

        var report = TakeOne(store);

        Assert.Equal(StationOutcome.CountsWrong, report.Outcome);
        Assert.Equal(AllUnits, store.TotalUnits);
        Assert.Equal(new Placed(Bag, EggAt, Eggs), store.At(Egg));
        Assert.Contains(report.Notes, note => note.Contains("could not be read"));
    }

    public static TheoryData<string> EveryStepThenUnreadable() => new()
    {
        nameof(IStationStore.IsStation),
        nameof(IStationStore.CountsIn),
        nameof(IStationStore.SetCount),
        nameof(IStationStore.MoveTo),
        nameof(IStationStore.AddStack),
        nameof(IStationStore.Remove),
        nameof(IStationStore.FreeCell),
        FakeStationStore.Replay,
    };

    /// <summary>Once the item cannot be read the steps stop: units may be left where the last step put them, but none is ever made.</summary>
    [Theory]
    [MemberData(nameof(EveryStepThenUnreadable))]
    public void Moving_into_a_station_never_makes_units_when_the_item_turns_unreadable(string step)
    {
        var store = Kitchen().Failing(nameof(IStationStore.AddStack), Fault.ReturnsNothing, times: 1).Failing(nameof(IStationStore.MoveTo), Fault.Throws).UnreadableAfter(step);

        var report = TakeOne(store);

        AssertNothingMadeWhenUnreadable(store, report);
    }

    [Theory]
    [MemberData(nameof(EveryStepThenUnreadable))]
    public void Sending_back_never_makes_units_when_the_item_turns_unreadable(string step)
    {
        var store = EggsLeftInThePot().UnreadableAfter(step);

        var report = SendBack(store);

        AssertNothingMadeWhenUnreadable(store, report);
    }

    private static void AssertNothingMadeWhenUnreadable(FakeStationStore store, StationReport report)
    {
        Assert.True(store.TotalUnits <= AllUnits, $"{store.TotalUnits} units, {AllUnits} at the start");
        Assert.Equal(PlayersEggs, store.At(PlayersStack)?.Count);
        Assert.Empty(store.RemovedNotMade);

        if (store.UnreadableReads > 0) Assert.Contains(report.Outcome, new[] { StationOutcome.CountsWrong, StationOutcome.Unreadable });
    }

    private static void AssertRuleHeld(FakeStationStore store, StationReport report)
    {
        Assert.Equal(AllUnits, store.TotalUnits);
        Assert.All(store.InStations, item => Assert.Equal(1, item.Count));
        Assert.Equal(PlayersEggs, store.At(PlayersStack)?.Count);
        Assert.Empty(store.RemovedNotMade);

        if (store.ThrowsFired > 0) Assert.NotEmpty(report.Notes);
    }
}
