# Stacking

Raise stack limits without fresh food merging into old food, split stacks with a key held, and have cooking and crafting count every item in a stack.

In Survival Log most seeds and food take one bag slot each, so a few days of farming fill every storage you own. Stacking lets you raise the stack limit for each item category, up to 999, without letting a fresh meal merge into a rotting one. When you first install it, every limit stays at the game's limit, so nothing stacks higher until you set a limit for a category. Each setting suggests a number, such as 99 for materials and seeds. The juicer, the cooking bench and the crafting table count every item in a stack, a stack never sits whole inside a cooking or crafting station, and holding Shift, Ctrl or Ctrl+Shift while you drag or right-click a stack splits it.

## Features

- **Stack limits per category**: each item category (Seed, Food, Material, Tool and the rest) has its own setting. Every category keeps the game's limit until you set one. Each setting in the config shows the game's limit for its category and a suggested number:
  - 99 for materials and seeds
  - 30 for renovation materials
  - 20 for food, fertilizer and fuel
  - 10 for meds, household items, plants and throwables

  Furniture, tools, books, story items and other one-off items get no suggestion. A number at or below the game's limit changes nothing, because a setting can only raise a limit.
- **Fresh food stays apart**: without the mod, the game doesn't look at freshness when it stacks items, so a bigger limit alone would let a fresh meal merge into a rotting one. For items the game never stacked, Stacking only puts items together when they have the same freshness left (to within an hour) and the same uses left. A meal that matches no stack starts a new one.
- **Items that never spoil stack together**: seeds, fuel, materials and anything else without a shelf life join a stack of their own kind no matter when you picked them up, or whether you found them on the map. Only their uses have to match.
- **Dishes and items with uses keep their limit**: dishes from cooking recipes, tools whose uses run out and anything similar stay at the game's limit. Dishes never stack because each dish holds its own number of servings, and the game takes one serving at a time from it. Reusable items you use directly, such as dyes and the comb, keep their limit too, because the game would take one from the stack each time you use it. Reusable materials such as wire do stack.
- **Cooking counts every item**: without the mod, three strawberries in one stack make one smoothie, while three loose ones make three. With it, a stack of three counts as three. When the cooking finishes, it uses up every item the dishes needed and leaves an empty can or bottle for each one, as the game normally does. Appliances and dishes with portions still count as one.
- **No stacks in cooking or crafting stations**: moving a stack into a station puts in one item, and the rest stays where it was. A second item takes a second slot, so two eggs take two slots, never one stack of two. With no free slot, nothing moves. This goes for drags, quick moves, the game's fill buttons and moves made by other mods, such as Storage Hub's organize and put-away. Moving a whole bag into a station puts one item from each stack there and sends the rest back to a free spot in the bag it came from.
- **The crafting table counts every item in a stack**: a recipe that needs two glass can be made from a stack of eight. The fill button and batch crafting take single items off a stack onto the bench, one per slot, and the rest stays where it was.
- **The shredder shreds every item in a stack**: without the mod, a stack in the shredder is shredded all at once and gives back what one item would. With it, the shredder takes one item off the stack per shred, gives that item's full yield, and goes on to the next until the stack is gone.
- **One item at a time into the shredder, compressor and dye vat**: a quick-move click on a stack in a material rack or a storage tab of these windows puts one item in, and the rest stays where it was.
- **Splitting by drag**: hold a key while you drop a stack in a bag or a storage to move only part of it. Shift moves half, Ctrl moves one, and Ctrl+Shift asks how many. Drop on a free spot to start a new stack, or on a stack of the same item to top it up as far as its limit allows. The new stack keeps the old one's freshness, uses and quality. This works in the bag, in storages, in vehicle trunks, and in the cooking, crafting table, brewing, compressor, dye, drone hub and shop windows.
- **Joining stacks**: drop a stack on a stack of the same item with no key held, and as much as fits joins it. The rest stays where it was. Without the mod, the game only swaps the two. This also works in the merchant's trade window, in your bag and on the drone. An offer that gains or loses items is updated to the new amount, and a whole offered stack dropped onto another is taken off the offer first. Dropping a stack on food of a different freshness, on a full stack or on a different item works as it normally does.
- **Splitting by right-click**: Shift, Ctrl or Ctrl+Shift with a right-click splits part of the stack onto the free spot nearest to it, in the same bag or storage. Food never lands on a moldy cell.
- **Offering part of a stack**: in the merchant's trade window, drag a stack from your bag onto the drone, or right-click it, with Shift (half), Ctrl (one) or Ctrl+Shift (choose). That part splits off in your bag and is offered, and the rest stays. An offer the merchant refuses goes back on its stack. The same keys with a drag from the drone to your bag, or a right-click on the drone, take only that part of an offer back. A split drag inside the drone or inside your bag splits there, and the part lands where you drop it. A plain right-click or a Shift left-click on a stack on your side offers one item, from a material rack too. Each click after that adds one more to the offer.
- **A how-many panel**: Ctrl+Shift opens a small panel. Type a number, drag the slider, or pick 1, Half or Max, then press Enter or Split. Esc, Cancel or a click outside closes it and nothing moves.
- **Short messages**: when the game would refuse a split (no room, a full stack, too heavy for the container, food of a different freshness), nothing moves and a short message on screen says why.
- **Ctrl-click doesn't open item details**: a click on an item with Ctrl held never opens the item's details window, in any window: the bag on its own or beside a storage, the merchant's trade window, the shop, the stations and the codex. In the bag, a Ctrl-click still uses an item at once where the game allows it. A plain click opens the details as before.
- **A stacking log**: what the mod did and anything that went wrong is written to `BepInEx\stacking.log`. On the first start it also lists every item category with its item count, the game's limits and how many items were raised.

Locked bags, category filters and take-out-only boxes still refuse a split the same way they refuse a move.

| Hold while you drop a stack | What moves |
| --- | --- |
| Shift | Half the stack, rounded down |
| Ctrl | One item |
| Ctrl + Shift | A panel asks how many |
| Nothing | The whole stack, like it normally does. On a stack of the same item, as much as fits joins it |

| Right-click | Backpack open alone | Backpack beside a storage, trunk or second bag |
| --- | --- | --- |
| Plain | Does nothing, like it normally does | Sends the item to the other side, like it normally does |
| Shift | Splits half onto the nearest free spot | Splits half onto the nearest free spot in the container the item is in |
| Ctrl | Splits one onto the nearest free spot | Sends every item of that kind to the other side, like it normally does |
| Ctrl + Shift | Opens the how-many panel | Sends every item of that kind to the other side, like it normally does |

Stacking doesn't add any data of its own to your save and doesn't edit game files. The stacks it makes and the moves it does are real, though. They change the items in your bags and storages, and those are saved with your game. You can remove the mod at any time. Stacks bigger than the game's limit stay as they are in your save, and new items follow the game's limits again.

## How it works

**Stack limits** are set per category, up to 999. A number at or below the game's limit keeps the game's limit, and `0`, the default, means "the game's limit". Items the game already stacks are raised to your number. Items the game never stacked are only raised when the freshness check is working, so old and new food can never merge.

**Stations** are the cooking furniture (benches, pots, juicers and the rest), hot pots, brew barrels, the compressor and shredder trays, and the bench of the crafting table on screen. Compost bins, material racks and robot molds are not stations.

**When a part of the mod can't start**, that part works the way the game normally does instead of guessing:

- If the freshness check can't start, items the game never stacked keep a limit of 1, and only items the game already stacks are raised.
- If the cooking count can't start, cooking counts a stack as one ingredient until you restart the game.
- Stack limits wait until the game's cooking recipe table is loaded, so no dish is ever raised by mistake. If a game update changes that table so it can't be read, no limit is raised at all, and the log says the limits are waiting for it.
- If a split or a station move fails part way, the items are put back, or the rest goes to a free spot in the bag it came from. Nothing ever lands in a station as a whole stack.
- If the crafting table part can't start, the crafting table counts and fills with whole stacks only, as it does without the mod. If a single item can't be put on the bench, the items already taken go back and the fill works as it does without the mod.
- If the drag part can't start, drags move whole stacks as they did before.
- If the item details part can't start, a Ctrl-click opens the item's details as it does without the mod.

Every failure is written to `BepInEx\stacking.log`, including any item that couldn't be put back.

## Known issues and limits

- Dishes that already share a stack from an earlier version stay in that stack.
- In the merchant's trade window a Ctrl-click does nothing. To offer every stack of a kind at once, click with the game's highlight key held, as without the mod.
- Dropping a stack on the floor always moves the whole stack, whatever keys you hold. Shift+Q still drops one item, like it normally does.
- Items that carry their own weight never stack in the game. A right-click never splits them, and a drag moves them as the game normally does.
- Compost bins and robot molds react to every change in them, so a split into or out of one is refused. A material rack allows a split out of it or inside it, but refuses a split into it from somewhere else. Split the stack in your bag first, then drag the part you want.
- What a cooking or crafting station holds is never split by a right-click.
- Items the game already stacks still stack in a station as far as the game allows. The station rule only covers items this mod made stackable. A Shift or Ctrl drag of those items into a station is refused instead of split, so drag them with no key held to move them normally.
- Stacks put in a station by version 0.2.0 stay there whole, and still cook in full.
- Dropping a stack on an item inside a station puts one item in a free slot the game picks, not in that item's slot, and the item that was there stays. Dropping a station item on a stack outside does nothing.
- With the backpack open alone, a Shift or Ctrl right-click uses the game's drop key to find the item under the mouse. That never drops anything. But if you press the drop key yourself within about half a second of a right-click on an empty cell (longer at a low frame rate), that one press may do nothing, so press it again. The first Shift or Ctrl right-click after one on an empty cell may also do nothing. Click again.
- Holding Ctrl changes how the bag looks. That is the game's Ctrl highlight and has no effect on the split.
- The game treats a very short drag (a few pixels) as a click, so holding Shift or Ctrl on it does the game's quick transfer or quick use instead of a split. On an item with no quick use, a short Ctrl drag does nothing.
- A plain drop into a cooking or crafting station, a compost bin, a material rack or a robot mold never joins stacks. It moves the stack as the game normally does.
- A cook started with whole stacks counted and finished after you turn `CountWholeStacks` off, or after you remove the mod, finishes the way the game normally does. That one cook takes only one item from each stack, so its extra dishes come free.
- The game keeps freshness to the hour, so two items up to an hour apart in freshness can share a stack.
- Carry weight is not part of this mod. Keep SurvivalLog CapacityMod installed if you use it for carry weight, furniture weight and batteries.

## Requirements

- Survival Log 1.1.20719. It was built for this version, so a game update can break it until the mod is updated.
- The [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12), the BepInEx 6 (IL2CPP) build for the game.
- Optional: [Mod Manager](https://www.nexusmods.com/survivallog/mods/73). With it, you can change every setting in game on its Mods screen and see what each one does, without opening the config file.
- Don't use it with BaseButler. Both change stack limits and they will fight, so remove BaseButler before installing this.
- [Exiled's Toolkit](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit) 0.20.0 or newer. Stacking does not load without it. It shows the mod's short messages as toasts, applies settings changed on the Mod Manager's Mods screen without a restart, and its UI size setting sizes the panel that asks how many to split. Stacking's `UiScale` setting under Interface sets a size for this mod alone. At 0, the default, it follows the Toolkit's size.

## Install

1. Install the [BepInEx Pack for Survival Log](https://www.nexusmods.com/survivallog/mods/12) (if no other mods were installed before, start the game once so BepInEx finishes setup, then quit).
2. Install Exiled's Toolkit. Download `ExiledToolkit-x.y.z.zip` from [its latest release](https://github.com/AnExiledDev/SurvivalLog-ExiledToolkit/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/61), and extract it into the game folder (the folder with `SurvivalLog.exe`).
3. Download `Stacking-x.y.z.zip` from the [latest release](https://github.com/AnExiledDev/SurvivalLog-Stacking/releases/latest) or from [Nexus Mods](https://www.nexusmods.com/survivallog/mods/74).
4. Extract the zip into the game folder (the folder with `SurvivalLog.exe`). It holds a `BepInEx` folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
   `C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\Stacking.dll`
5. If you use BaseButler, delete its DLL from `BepInEx\plugins`.

## Uninstall

Delete `Stacking.dll` from the `BepInEx\plugins` folder. For a clean slate, also delete `BepInEx\config\com.dev.survivallog.stacking.cfg` and `BepInEx\stacking.log`. Stacks already bigger than the game's limit stay in your save as they are.

## Configuration

Settings live in `BepInEx\config\com.dev.survivallog.stacking.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Cooking CountWholeStacks and Splitting Enabled take effect at the next game start. Each setting in the cfg file is explained in English and Simplified Chinese. See [CONFIG.md](CONFIG.md) for every setting.

The split panel and the messages a split shows follow the game's language, English or Simplified Chinese.

A config file you already have keeps its saved numbers, so an update never changes your limits. To go back to the game's limits, delete its `[StackLimits]` section (or the whole file) while the game is closed.

## Troubleshooting

Built against BepInEx `6.0.0-be.788`.

Check `BepInEx\LogOutput.log` for a line starting `===== Stacking 0.8.6 started`. A line starting `stack limits:` lists the limits in use, for example `Food 20, Drug 10, DailyNecessities 10, Material 99, Seed 99, ...`, and shows up again each time you change one. The lines after it say which parts of the mod started. Any part that couldn't start is named there, along with what works the way the game normally does instead.

The same lines, plus every split, every station move and every failure, are in `BepInEx\stacking.log`, which survives restarts. When that file passes 1 MB, it is renamed `stacking.log.1` and a new one starts.

If stacks still stop at the game's limits, check that BaseButler is removed and that the category's setting is above the game's limit (each setting in the config shows the game's limit).

## Feedback and bug reports

Found a bug, have an idea, or something feels off? Come say so on the Core Directive Discord: https://discord.gg/trTsyxUFqg . You can also post in the Posts tab of the mod's Nexus Mods page. When reporting a bug, attach `BepInEx\LogOutput.log` and `BepInEx\stacking.log`.

To reach me directly, message `exiled.dev` on Discord. You can find me in the [official Survival Log Discord](https://discord.gg/MZGg4gQAUe). I'm also on X as [@AnExiledDev](https://x.com/AnExiledDev).

A Survival Log wiki with detailed documentation for all of my mods is coming soon.

## Build

This is a BepInEx 6 IL2CPP plugin. It compiles against BepInEx and the game's IL2CPP interop assemblies, so a game install with BepInEx set up and started once is required. Those assemblies come from the game and are not part of this repo. The .NET 8 SDK is required.

```
dotnet build src/Stacking -c Release -p:GameDir="C:\path\to\Survival Log"
```

`GameDir` is the game folder. The default in the project file is a local path you will almost certainly need to override. The output DLL is at `src\Stacking\bin\Release\Stacking.dll`.

The unit tests cover the stack rules (stack limits, freshness, cooking counts, station moves, splitting and Ctrl-clicks) and need nothing from the game:

```
dotnet test tests/Stacking.Tests
```

## License

MIT. Copyright (c) 2026 AnExiledDev. See [LICENSE](LICENSE).
