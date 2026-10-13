# Stacking settings

Settings live in `BepInEx\config\com.dev.survivallog.stacking.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## StackLimits

Each setting is the most items of one item category in one stack. The setting names are the game's own category names; the second column is the name the game shows.

| Setting | Shown in game as | Game limit | Default | For example |
| --- | --- | --- | --- | --- |
| `Food` | Food | 1 | `20` | Cookies, Bread, Canned Luncheon Meat |
| `Drug` | Meds | 1 | `10` | Vitamins, Bandage, Adhesive Bandage. Vitamins and some other meds spoil like food, so only meds with the same freshness share a stack. |
| `Books` | Books | 1 | `0` | Woodworking Basics, Gardening Manual |
| `ElectricalAppliances` | Electronics | 1 | `0` | Game Console |
| `DailyNecessities` | Household | 1 | `10` | Cigarettes, Paper Crane, Paper Airplane |
| `Tool` | Tools | 1 | `0` | Lockpick, Crowbar, Pliers |
| `Pet` | Pets | none | `0` | The game has no items in this category. |
| `Pack` | Packaging | 1 | `0` | Random Materials Pack (x8) |
| `Material` | Materials | 1 | `99` | Wood Chips, Scrap Metal, Wire |
| `Seed` | Seeds | 1 | `99` | Carrot Seeds, Potato Seeds, Tomato Seeds |
| `Fertilizer` | Fertilizer | 1 | `20` | Basic Fertilizer, Compound Fertilizer |
| `Trap` | Trap | 1 | `0` | Simple Mousetrap, Iron Animal Trap. Traps can be used again and again, so they keep the game's limit. |
| `Fuel` | Fuel | 1 | `20` | Gas Canister, Diesel, Common Alcohol |
| `BagFurniture` | Furniture Packages | 1 | `0` | Old Guitar Package, Shredder Package |
| `Flower` | Plants | 1 | `10` | Daisy, Pansy, Rose |
| `Throwable` | Throwables | 1 | `10` | Molotov Cocktail, Rotten Meat Strip. Rotten meat spoils like food, so only pieces with the same freshness share a stack. |
| `RobotModule` | Robot Modules | 1 | `0` | Compact Battery Pack, Harvest Module |
| `StoryItem` | Story Items | 1 | `0` | Mysterious Envelope, Note |
| `Collectible` | Collectibles | 1 | `0` | Gold Bar, Diamond Ring, Artistic Sculpture |
| `Record` | Records | 1 | `0` | "The Long Night", "The Way Home" |
| `Renovation` | Renovation Materials | 1 to 10 | `30` | Wallpaper and flooring (10 in the game). Dyes can be used again and again, so they keep the game's limit. |
| `TaskItem` | Quest Items | 1 | `0` | Garage Key, Cold Storage Key |

Each takes a whole number from 0 to 999; a bigger number counts as 999 and a negative one as 0. `0`, or any number at or below the game's own limit, keeps the game's limit: a setting only ever raises a limit, it never lowers one. Items with portions or more than one use always keep the game's limit.

For items the game never stacked (most food, for example), only items with the same freshness left (within an hour) and the same uses share a stack. Seeds of one kind usually share a stack, since they never spoil.

## Cooking

| Setting | Default | What it does |
| --- | --- | --- |
| `CountWholeStacks` | `true` | A cooking bench counts every item in a stack as an ingredient, like loose items, and the finished cook uses up the items its dishes took. `false` counts a stack as one ingredient, as in the base game. |

A cook that was started with this on and finishes after you turn it off is settled the game's way: that one cook takes only one item from each stack, so its extra dishes come free.

## Splitting

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Hold Shift while you drop a stack to move half, Ctrl to move one, or Ctrl+Shift to choose how many. A right-click with the same keys splits the stack onto the nearest free spot in the same bag or storage. `false` makes drags and right-clicks work as in the base game. |

Turning splitting off does not change the station rule: a stack moved into a cooking or crafting station still puts in one item.
