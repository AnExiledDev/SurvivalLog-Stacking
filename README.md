# Stacking

Bigger stacks that keep fresh food apart from old food, cooking that counts every item in a stack, and a way to split stacks.

In Survival Log most seeds and food take one bag slot each, so a few days of farming fill every storage you own. Stacking raises the stack limit per item category (materials and seeds to 99 and food to 20 to start with) without letting a fresh meal merge into a rotting one. A juicer or cooking bench counts every item in a stack, stacks never sit whole inside a cooking or crafting station, and Shift, Ctrl or Ctrl+Shift splits a stack when you drag or right-click it.

## Features

- **Stack limits per category**: each item category (Seed, Food, Material, Tool and the rest) has its own setting. Materials and seeds start at 99; food, fertilizer and fuel at 20; renovation materials at 30; meds, household items, plants and throwables at 10. Furniture, tools, books, story items and other one-off items keep the game's limit until you set one. Each setting in the config shows the game's own limit for its category. A limit only ever raises the game's, it never lowers one.
- **Fresh food stays apart**: the game's own stack search ignores freshness, so a bigger limit alone would let a fresh meal merge into a rotting one. For items the game never stacked, the mod only stacks items with the same freshness left (within an hour) and the same uses. A meal that matches nothing starts its own stack, as the game did before.
- **Seeds of one kind stack together**: seeds never spoil, so seeds of one kind usually join the same stack.
- **Items with portions or uses keep their limit**: dishes with portions, tools with uses and anything similar are left at the game's limit.
- **Cooking counts every item**: without the mod, three strawberries in one stack make one smoothie, where three loose ones make three. With it, a stack of three counts as three, and a finished cook uses up every item its dishes took, leaving cans and bottles behind for each one as the game does. Appliances and dishes with portions still count as one.
- **No stacks in cooking or crafting stations**: moving a stack into a station puts in one item and the rest stays where it was. A second item takes a second slot, so two eggs take two slots, never one stack of two. With no free slot, nothing moves. This holds for drags, quick moves, the game's fill buttons and moves made by other mods (such as Storage Hub's organize and put-away). Moving a whole bag into a station keeps one of each stack there and sends the rest back to a free spot in the bag it came from.
- **Splitting by drag**: hold Shift, Ctrl or both while you drop a stack in a bag or a storage to move only part of it (see the table below). Drop on a free spot to start a new stack, or on a stack of the same item to top it up as far as its limit allows. The new stack keeps the old one's freshness, uses and quality. This works in the bag, in storages, in vehicle trunks, and in the cooking, crafting table, brewing and shop windows.
- **Splitting by right-click**: Shift, Ctrl or Ctrl+Shift with a right-click splits the part off onto the free spot nearest the stack, in the same bag or storage. Food never lands on a moldy cell.
- **A how-many panel**: Ctrl+Shift opens a small panel. Type a number, drag the slider, or pick 1, Half or Max, then press Enter or Split. Esc, Cancel or a click outside closes it and nothing moves.
- **Short messages**: a split the game would refuse (no room, a full stack, too heavy for the container, food of a different freshness) moves nothing and says why in a short message at the bottom of the screen.
- **A stacking log**: what the mod did and anything that went wrong is written to `BepInEx\stacking.log`. On the first start it also lists every item category with its item count, the game's own limits and how many items were raised.

The game's own checks still apply: locked bags, category filters and take-out-only boxes refuse a split the same way they refuse a move.

| Hold while you drop a stack | What moves |
| --- | --- |
| Shift | Half the stack, rounded down |
| Ctrl | One item |
| Ctrl + Shift | A panel asks how many |
| Nothing | The whole stack, as the game does |

| Right-click | Backpack open alone | Backpack beside a storage, trunk or second bag |
| --- | --- | --- |
| Plain | Nothing, as in the game | Sends the item to the other side, as in the game |
| Shift | Splits half onto the nearest free spot | Splits half onto the nearest free spot in the item's own container |
| Ctrl | Splits one onto the nearest free spot | The game's own "send every item of this kind" |
| Ctrl + Shift | Opens the how-many panel | The game's own "send every item of this kind" |

Stacking adds no data of its own to your save and does not edit game files. The stacks it makes and the moves it does are real, though: they change the items in your bags and storages, and those are saved with your game. You can remove the mod at any time. Stacks bigger than the game's limit stay as they are in your save; new items follow the game's limits again.

## How it works

**Stack limits** are set per category, up to 999. A number at or below the game's own limit keeps the game's limit, and `0` means "the game's limit". Items the game already stacks are simply raised to your number. Items the game never stacked only get raised when the freshness check is in place, so old and new food can never merge.

**Stations** are found by the game's own tests: cooking furniture (benches, pots, juicers and the rest), hot pots, brew barrels, the compressor and shredder trays, and the bench of the crafting table on screen. Compost bins, material racks and robot molds are not stations.

**When something fails**, the mod falls back to the game's own behavior rather than guess:

- If the freshness check cannot be put in place, items the game never stacked keep a limit of 1, and only items the game already stacks get raised.
- If the cooking count cannot be put in place, cooking goes back to the game's own counting for that session.
- If a split or a station move fails part way, the items are put back, or the rest goes to a free spot in the bag it came from. Nothing ever lands in a station as a whole stack.
- If the drag hooks cannot be put in place, drags move whole stacks as they did before.

Every failure is written to `BepInEx\stacking.log`, including any item that could not be put back.

## Known issues and limits

Moving items around is finicky. This mod moves, splits and merges real items in your bags, storages and stations, and the game was not built for stacks of food. **Back up your save before trying it for the first time**, and keep a backup while you play with a new version. If an item goes missing, doubles, or lands somewhere odd, please tell me (see [Feedback](#feedback-and-bug-reports)), even if you are not sure the mod caused it.

- Version 0.4.0 has not been played through in the game yet.
- Dropping a stack on the floor moves the whole stack whatever you hold, and Shift+Q is still the game's own drop-one key. Items that carry a weight of their own are never split by a right-click, and never stack in the game, so a drag of one stays the game's own move.
- Compost bins, material racks and robot molds run their own code on every change, so a split into or out of one is refused. Split the stack in your bag first, then drag the part you want.
- What a cooking or crafting station holds is never split by a right-click.
- A recipe's fill button puts in one of each stack it picks, so a recipe that needs two of an item may need a second press.
- Items the game already stacks still stack in a station as the game allows; the station rule only covers items this mod made stackable. A Shift or Ctrl drag of them into a station is refused rather than split; drag without a key held to move them as the game does.
- Stacks put in a station by version 0.2.0 stay there whole, and still cook in full.
- Dropping a stack on an item inside a station puts one item in whichever free slot of the station the game picks, not in that item's slot, and the station item stays. Dropping a station item on a stack outside does nothing.
- A Shift or Ctrl right-click with the backpack open alone asks the game which item is under the mouse through its drop key. That never drops anything. If you press the drop key yourself right after a right-click on an empty cell (within about half a second, longer at a low frame rate), that one press may do nothing; press it again. The first Shift or Ctrl right-click after one on an empty cell may do nothing too; click again.
- Holding Ctrl changes how the bag looks. That is the game's own Ctrl highlight and has no effect on the split.
- A very short drag (a few pixels) is a click to the game, so Shift or Ctrl held on it does the game's own quick transfer or quick use, not a split.
- Dragging a whole stack onto another without a key held is the game's own merge and is not checked by the mod. What that merge does with food of different freshness has not been checked in the game.
- A cook started with whole stacks counted and finished after you turn `CountWholeStacks` off, or after you remove the mod, is settled the game's way: that one cook takes only one item from each stack, so its extra dishes come free.
- The game keeps freshness to the hour, so two items up to an hour apart in freshness can share a stack.
- Carry weight is not part of this mod. Keep SurvivalLog CapacityMod installed if you use it for carry weight, furniture weight and batteries.

## Requirements

- Survival Log 1.1.18317. Built for this version; a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Not together with BaseButler. Both change stack limits and they will fight; remove BaseButler before installing this.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Download `Stacking.dll` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-Stacking/releases/latest) and put it in the `BepInEx\plugins` folder inside the game folder (the folder with the game .exe). Full path example: `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Stacking.dll`
3. If you use BaseButler, delete its DLL from `BepInEx\plugins`.

## Uninstall

Delete `Stacking.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.stacking.cfg` and `BepInEx\stacking.log`. Stacks already bigger than the game's limit stay in your save as they are.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.stacking.cfg`, created the first time the game starts with the mod. Edit it while the game is closed; changes apply at the next game start. See [CONFIG.md](CONFIG.md) for every setting.

A config file you already have keeps its saved numbers. To get the new default stack limits, delete its `[StackLimits]` section (or the whole file) while the game is closed.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Stacking 0.4.0 started`. It lists the stack limits in use, for example `Food 20, Drug 10, DailyNecessities 10, Material 99, Seed 99, ...`. The lines after it say which game hooks are in place; a hook that could not be put in place is named there, with what falls back to the game's own behavior.

The same lines, plus every split, every station move and every failure, are in `BepInEx\stacking.log`, which survives restarts. When that file passes 1 MB, it is cut down to its newest part at the next game start.

If stacks still stop at the game's limits, check that BaseButler is removed and that the category's setting is above the game's own limit (each setting in the config shows the game's limit).

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also leave a comment on the Nexus page or [open an issue](https://github.com/AnExiledDev/SurvivalLog-Stacking/issues). When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\stacking.log`.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/Stacking -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\Stacking\bin\Release\Stacking.dll`.

The unit tests cover the pure rules (stack limits, freshness, cooking counts, station moves and splitting) and need nothing from the game:

```
dotnet test tests/Stacking.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
