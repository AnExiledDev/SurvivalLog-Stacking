# Stacking

A BepInEx 6 (IL2CPP) plugin for Survival Log that raises stack limits per item category, so seeds and food stop taking one slot each, without letting food of different freshness merge.

## What it does

- **Stack limits per category.** Each item category (Seed, Food, Material, Tool and the rest) gets a config entry. Seeds default to 99 and food to 20; every other category keeps the game's limit until you set one. A limit only ever raises the game's; it never lowers one.
- **Freshness stays honest.** The game's own stack search ignores freshness, so raising a limit alone would let a fresh meal merge into a rotting one. For items the game never stacked, this mod narrows that search to stacks with the same shelf life left (within an hour) and the same uses. A meal that matches nothing starts its own stack, which is what the game did before.
- **Items with portions or uses keep their limit.** The game merges portions of a dish only while its limit is 1, so dishes with portions, tools with uses and anything similar are left alone.
- **Fails safe.** If the stack search cannot be guarded, items the game never stacked keep a limit of 1, and only items the game already stacks get raised.

Weight limits are not part of this release. SurvivalLog CapacityMod keeps handling carry weight, furniture weight and batteries; keep it installed.

## Install

Copy `Stacking.dll` into `BepInEx/plugins`. Remove BaseButler if it is still there; two stacking mods will fight.

## Config

`BepInEx/config/com.dev.survivallog.stacking.cfg`, section `[StackLimits]`, one entry per category:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Seed` | 99 | Most seeds in one stack |
| `Food` | 20 | Most food items in one stack |
| every other category | 0 | 0 keeps the game's limit |

Limits are capped at 999. On the first load, `BepInEx/stacking.log` lists every category with its item count and the game's own limits, so you can see what a number would change before you pick it.

## Known limits

- Dragging one stack onto another by hand is not guarded. The game's portion merge skips items whose limit is above 1, but what a drag does for a newly stackable item has not been checked in the game.
- Stacks larger than the game's limit stay in your save. Removing the mod keeps them as they are; new items follow the game's limits again.
- Adding an item drops the fraction of an hour the game tracks, so two items up to an hour apart in freshness can share a stack.
- 0.1.0 has not been run in the game yet.

## Build

```
dotnet build -c Release src/Stacking
dotnet test tests/Stacking.Tests
```

The project expects the game at `D:\Downloads\Survival-Log-AnkerGames\Survival Log`; pass `-p:GameDir=...` for another path.

## License

MIT, see [LICENSE](LICENSE).
