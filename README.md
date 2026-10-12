# Stacking

A BepInEx 6 (IL2CPP) plugin for Survival Log that raises stack limits per item category, so seeds and food stop taking one slot each, without letting food of different freshness merge. Stacks count in full when cooking, and you can split them with a modifier key.

## What it does

- **Stack limits per category.** Each item category (Seed, Food, Material, Tool and the rest) gets a config entry. Seeds default to 99 and food to 20; every other category keeps the game's limit until you set one. A limit only ever raises the game's; it never lowers one.
- **Freshness stays honest.** The game's own stack search ignores freshness, so raising a limit alone would let a fresh meal merge into a rotting one. For items the game never stacked, this mod narrows that search to stacks with the same shelf life left (within an hour) and the same uses. A meal that matches nothing starts its own stack, which is what the game did before.
- **Items with portions or uses keep their limit.** The game merges portions of a dish only while its limit is 1, so dishes with portions, tools with uses and anything similar are left alone.
- **Cooking counts every unit.** The game tallies a cooking bench one stack at a time, so three strawberries in one stack made one smoothie where three loose ones made three. Now a stack of three counts as three, and a finished cook uses up every unit its dishes took, leaving behind cans and bottles for each one as the game does. Appliances and dishes with portions still count as one.
- **Splitting stacks.** Hold a modifier while you drop a stack in a bag or a storage:

| Hold | What moves |
| --- | --- |
| Shift | Half the stack, rounded down |
| Ctrl | One unit |
| Ctrl + Shift | A panel asks how many: type a number, drag the slider, or pick 1, Half or Max. Enter or Split moves them; Esc, Cancel or a click outside closes it and nothing moves |
| nothing | The whole stack, as the game does |

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
| `Splitting` | `Enabled` | true | Off: a drag moves the whole stack, as without this mod |

Limits are capped at 999. On the first load, `BepInEx/stacking.log` lists every category with its item count and the game's own limits, so you can see what a number would change before you pick it.

## Known limits

- Splitting works between bags and storages only. Dropping a stack on the floor moves the whole stack whatever you hold, and Shift+Q is still the game's own drop-one key.
- Cooking benches, compost bins, material racks, crafting slots, robot molds and hot pots run their own code on every change, so a split into or out of one is refused. Split the stack in your bag first, then drag the part you want.
- Items that carry a weight of their own never stack in the game, so a drag of one stays the game's own move.
- Holding Ctrl changes how the bag looks. That is the game's own Ctrl highlight and has no effect on the split.
- A drag shorter than a few pixels is a click to the game, so Shift or Ctrl held on it does the game's own quick transfer or quick use, not a split.
- Dragging a whole stack onto another without a modifier is the game's own merge and is not guarded. The game's portion merge skips items whose limit is above 1, but what that merge does with food of different freshness, for an item the game never stacked, has not been checked in the game.
- A cook started with whole stacks counted and finished after `Cooking/CountWholeStacks` is turned off, or after the mod is removed, is settled the game's way: that one cook takes only one unit from each stack, so its extra dishes come free.
- Stacks larger than the game's limit stay in your save. Removing the mod keeps them as they are; new items follow the game's limits again.
- Adding an item drops the fraction of an hour the game tracks, so two items up to an hour apart in freshness can share a stack.
- 0.2.0 has not been run in the game yet. Back up your save before trying it.

## Build

```
dotnet build -c Release src/Stacking
dotnet test tests/Stacking.Tests
```

The project expects the game at `D:\Downloads\Survival-Log-AnkerGames\Survival Log`; pass `-p:GameDir=...` for another path.

## License

MIT, see [LICENSE](LICENSE).
