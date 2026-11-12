# Stacking settings

Settings live in `BepInEx\config\com.dev.survivallog.stacking.cfg`, created the first time the game starts with the mod. Change them in game on the Mod Manager's Mods screen, where they take effect at once, or edit the file while the game is closed. Cooking CountWholeStacks and Splitting Enabled take effect at the next game start. Delete the file to get the defaults back.

## StackLimits

Each setting is the most items of one item category that fit in one stack. The setting names are the category names in the game's files, and the second column is the name the game shows on screen. Every one defaults to `0`, the game's limit, so no stack changes until you set one. The Suggested column is a good place to start.

| Setting | Shown in game as | Game limit | Default | Suggested | For example |
| --- | --- | --- | --- | --- | --- |
| `Food` | Food | 1 | `0` | `20` | Cookies, Bread, Canned Luncheon Meat |
| `Drug` | Meds | 1 | `0` | `10` | Vitamins, Bandage, Adhesive Bandage. Vitamins and some other meds spoil like food, so only meds with the same freshness share a stack. |
| `Books` | Books | 1 | `0` |  | Woodworking Basics, Gardening Manual |
| `ElectricalAppliances` | Electronics | 1 | `0` |  | Game Console |
| `DailyNecessities` | Household | 1 | `0` | `10` | Cigarettes, Paper Crane, Paper Airplane |
| `Tool` | Tools | 1 | `0` |  | Lockpick, Crowbar, Pliers |
| `Pet` | Pets | none | `0` |  | The game has no items in this category. |
| `Pack` | Packaging | 1 | `0` |  | Random Materials Pack (x8) |
| `Material` | Materials | 1 | `0` | `99` | Wood Chips, Scrap Metal, Wire |
| `Seed` | Seeds | 1 | `0` | `99` | Carrot Seeds, Potato Seeds, Tomato Seeds |
| `Fertilizer` | Fertilizer | 1 | `0` | `20` | Basic Fertilizer, Compound Fertilizer |
| `Trap` | Trap | 1 | `0` |  | Simple Mousetrap, Iron Animal Trap. Traps can be used again and again, so they keep the game's limit. |
| `Fuel` | Fuel | 1 | `0` | `20` | Gas Canister, Diesel, Common Alcohol |
| `BagFurniture` | Furniture Packages | 1 | `0` |  | Old Guitar Package, Shredder Package |
| `Flower` | Plants | 1 | `0` | `10` | Daisy, Pansy, Rose |
| `Throwable` | Throwables | 1 | `0` | `10` | Molotov Cocktail, Rotten Meat Strip. Rotten meat spoils like food, so only pieces with the same freshness share a stack. |
| `RobotModule` | Robot Modules | 1 | `0` |  | Compact Battery Pack, Harvest Module |
| `StoryItem` | Story Items | 1 | `0` |  | Mysterious Envelope, Note |
| `Collectible` | Collectibles | 1 | `0` |  | Gold Bar, Diamond Ring, Artistic Sculpture |
| `Record` | Records | 1 | `0` |  | "The Long Night", "The Way Home" |
| `Renovation` | Renovation Materials | 1 to 10 | `0` | `30` | Wallpaper and flooring (10 in the game). Dyes can be used again and again, so they keep the game's limit. |
| `TaskItem` | Quest Items | 1 | `0` |  | Garage Key, Cold Storage Key |

Each takes a whole number from 0 to 999. A bigger number counts as 999 and a negative one as 0. `0`, or any number at or below the game's limit, keeps the game's limit, because a setting can only raise a limit. Dishes a cooking recipe can make, and items with more than one use that runs out, always keep the game's limit. Reusable items you use directly, such as dyes, a comb or dumbbells, keep the game's limit too, because the game would take one from the stack each time you use one. Reusable materials such as wire and iron rods stack like any other.

For items the game never stacked (most food, for example), only items with the same freshness left (within an hour) and the same uses share a stack. Items that never spoil, such as seeds, fuel and materials, share a stack whenever their uses match, no matter when they were picked up, and items found on the map share a stack with ones you made or bought.

## Cooking

| Setting | Default | What it does |
| --- | --- | --- |
| `CountWholeStacks` | `true` | A cooking bench counts every item in a stack as an ingredient, the same as loose items, and uses up every item the dishes needed when the cooking finishes. `false` counts a whole stack as one ingredient, as it does without the mod. |

A cook that was started with this on and finishes after you turn it off finishes the way the game normally does. That one cook takes only one item from each stack, so its extra dishes come free.

## Splitting

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Hold Shift while you drop a stack to move half, Ctrl to move one, or Ctrl+Shift to choose how many. A right-click with the same keys splits the stack onto the nearest free spot in the same bag or storage. `false` makes drags and right-clicks work like they do without the mod. |

Turning splitting off doesn't change the station rule, so a stack moved into a cooking or crafting station still puts in one item. A Ctrl-click still doesn't open item details either, whether splitting is on or off.

## Interface

| Setting | Default | What it does |
| --- | --- | --- |
| `UiScale` | `0` | How big Stacking's own windows, panels and messages are drawn, in percent, from 75 to 200. At `0` they follow the UI size set in Exiled's Toolkit (its `UiScale`). |
