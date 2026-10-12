# Stacking

A BepInEx 6 (IL2CPP) plugin for Survival Log that raises stack limits per item category, so seeds and food stop taking one slot each, without letting food of different freshness merge. Stacks count in full when cooking, and you can split them with a modifier key.

## What it does

- **Stack limits per category.** Each item category (Seed, Food, Material, Tool and the rest) gets a config entry. Seeds default to 99 and food to 20; every other category keeps the game's limit until you set one. A limit only ever raises the game's; it never lowers one.
- **Freshness stays honest.** The game's own stack search ignores freshness, so raising a limit alone would let a fresh meal merge into a rotting one. For items the game never stacked, this mod narrows that search to stacks with the same shelf life left (within an hour) and the same uses. A meal that matches nothing starts its own stack, which is what the game did before.
- **Items with portions or uses keep their limit.** The game merges portions of a dish only while its limit is 1, so dishes with portions, tools with uses and anything similar are left alone.
- **Cooking counts every unit.** The game tallies a cooking bench one stack at a time, so three strawberries in one stack made one smoothie where three loose ones made three. Now a stack of three counts as three, and a finished cook uses up every unit its dishes took, leaving behind cans and bottles for each one as the game does. Appliances and dishes with portions still count as one.
- **Seeds of one kind always stack together.** Seeds never spoil, so the freshness check skips any item whose shelf life is unset, and every seed of one kind joins the same stack. Food still keeps the freshness rule.
- **No stacks in cooking or crafting stations.** A stack moved into a station puts in one unit and the rest stays where it was; a second unit takes a second slot, so two eggs take two slots, never one stack of two. With no free slot, nothing moves. This holds for drags, quick moves, the game's fill buttons and moves made by other mods (Storage Hub's organize and put-away), because it is enforced at the game's own move points. A whole-bag move into a station keeps one unit of each stack there and sends the rest back to the nearest free spot in the bag it came from. If a step of any of these moves fails, the move is undone or the rest goes to a free spot in the bag it came from; no fallback ever puts a stack in a station, and `BepInEx/stacking.log` names any unit that could not be put back. Stations are found by the game's own tests: cooking furniture (benches, pots, juicers and the rest), hot pots, brew barrels, the compressor and shredder trays, and the bench of the crafting table on screen. Compost bins, material racks and robot molds are not stations.
- **Splitting stacks.** Hold a modifier while you drop a stack in a bag or a storage:

| Hold | What moves |
| --- | --- |
| Shift | Half the stack, rounded down |
| Ctrl | One unit |
| Ctrl + Shift | A panel asks how many: type a number, drag the slider, or pick 1, Half or Max. Enter or Split moves them; Esc, Cancel or a click outside closes it and nothing moves |
| nothing | The whole stack, as the game does |

The same keys work on a right-click, which splits the part off onto the free spot nearest the stack in the same container; with no free spot nothing moves and `BepInEx/stacking.log` says so. Food never lands on a moldy cell. Shift+right-click splits everywhere. Ctrl and Ctrl+Shift right-click with two bags open stay the game's own transfer-by-kind (Ctrl is the game's highlight key by default). A plain right-click does what the game does:

| Where | Plain right-click (the game's) | Shift, Ctrl or Ctrl+Shift right-click |
| --- | --- | --- |
| Backpack alone | Nothing, the tooltip stays | Splits in the backpack |
| Backpack beside a storage, trunk or second bag | Sends the item to the other side | Shift splits in the item's own container. Ctrl or Ctrl+Shift is the game's "send every item of this kind", which stays the game's |

Splitting by drag also works inside the cooking, crafting table, brewing and shop views, in the bag and between the bag and the other side. A split into a station follows the station rule, so it puts in one unit. Splitting a stack out of a vehicle trunk works too.

Drop on a free spot to start a new stack there, or on a stack of the same item to top it up as far as its limit allows. The new or topped-up stack keeps the dragged stack's freshness, uses and quality. A split the game would refuse (no room, a full stack, too heavy for the container, food of a different freshness) moves nothing and says why in a short message at the bottom of the screen. If the game cannot put a new stack on the spot you dropped it, it puts it elsewhere in the container, or on the ground when the container is full, and a message says which. The game's own checks still apply: locked bags, category filters and take-out-only boxes refuse a split as they refuse a move.

- **Fails safe.** If the stack search cannot be guarded, items the game never stacked keep a limit of 1, and only items the game already stacks get raised. If a cooking hook cannot be applied or throws, cooking goes back to the game's own counting for the session. If the drag hooks cannot be applied, drags move whole stacks as they did before; if only the picker or new-stack hook fails, those splits show a message and nothing moves. If a split fails part way, the counts are put back and nothing moves. Every failure is written to `BepInEx/stacking.log`.

Weight limits are not part of this release. SurvivalLog CapacityMod keeps handling carry weight, furniture weight and batteries; keep it installed.

## Install

Copy `Stacking.dll` into `BepInEx/plugins`. Remove BaseButler if it is still there; two stacking mods will fight.

## Config

`BepInEx/config/com.dev.survivallog.stacking.cfg`:

| Section | Setting | Default | Meaning |
| --- | --- | --- | --- |
| `StackLimits` | `Seed` | 99 | Most seeds in one stack |
| `StackLimits` | `Food` | 20 | Most food items in one stack |
| `StackLimits` | every other category | 0 | 0 keeps the game's limit |
| `Cooking` | `CountWholeStacks` | true | Off: a stack counts as one ingredient, as without this mod |
| `Splitting` | `Enabled` | true | Off: a drag moves the whole stack and a right-click does what the game does, as without this mod |

Limits are capped at 999. On the first load, `BepInEx/stacking.log` lists every category with its item count and the game's own limits, so you can see what a number would change before you pick it.

## Known limits

- Dropping a stack on the floor moves the whole stack whatever you hold, and Shift+Q is still the game's own drop-one key. Items that carry a weight of their own are never split by a right-click either.
- Compost bins, material racks and robot molds run their own code on every change, so a split into or out of one is refused. Split the stack in your bag first, then drag the part you want.
- What a cooking or crafting station holds is never split by a right-click.
- A recipe's fill button puts in one unit of each stack it picks, so a recipe that needs two of an item may need a second press.
- Items the game already stacks (fuel and the like) still stack in a station as the game allows; the station rule covers only items this mod made stackable. A Shift or Ctrl drag of them into a station is refused rather than split; drag without a modifier to move them as the game does.
- Stacks put in a station by 0.2.0 stay there whole and still cook in full, because `Cooking/CountWholeStacks` counts every unit.
- Dropping a stack on an item inside a station puts one unit in whichever free slot of the station the game picks, not in that item's slot, and the station item stays. Dropping a station item on a stack outside does nothing, because the trade would bring the stack in whole.
- A Shift or Ctrl right-click with the backpack alone asks the game which item is under the tooltip through its drop key. That never drops anything. If you press the drop key yourself before the game has answered, or within about half a second (longer at a frame rate under 60) of a right-click on an empty cell, that one press may do nothing; press it again. The first Shift or Ctrl right-click after one on an empty cell may do nothing too; click again.
- 0.3.1 has not been run in the game yet. Back up your save before trying it.
- Items that carry a weight of their own never stack in the game, so a drag of one stays the game's own move.
- Holding Ctrl changes how the bag looks. That is the game's own Ctrl highlight and has no effect on the split.
- A drag shorter than a few pixels is a click to the game, so Shift or Ctrl held on it does the game's own quick transfer or quick use, not a split.
- Dragging a whole stack onto another without a modifier is the game's own merge and is not guarded. The game's portion merge skips items whose limit is above 1, but what that merge does with food of different freshness, for an item the game never stacked, has not been checked in the game.
- A cook started with whole stacks counted and finished after `Cooking/CountWholeStacks` is turned off, or after the mod is removed, is settled the game's way: that one cook takes only one unit from each stack, so its extra dishes come free.
- Stacks larger than the game's limit stay in your save. Removing the mod keeps them as they are; new items follow the game's limits again.
- Adding an item drops the fraction of an hour the game tracks, so two items up to an hour apart in freshness can share a stack.

## Build

```
dotnet build -c Release src/Stacking
dotnet test tests/Stacking.Tests
```

The project expects the game at `D:\Downloads\Survival-Log-AnkerGames\Survival Log`; pass `-p:GameDir=...` for another path.

## License

MIT, see [LICENSE](LICENSE).
