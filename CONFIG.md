# Stacking settings

Settings live in `BepInEx\config\com.dev.survivallog.stacking.cfg`, created the first time the game starts with the mod. Edit the file while the game is closed; changes apply at the next game start. Delete the file to get the defaults back.

## StackLimits

Each setting is the most items of that category in one stack. The names are the game's own item categories.

| Setting | Default | What it does |
| --- | --- | --- |
| `Food` | `20` | Most food items in one stack. |
| `Seed` | `99` | Most seeds in one stack. |
| `Drug` | `0` | Most medicine items in one stack. |
| `Books` | `0` | Most books in one stack. |
| `ElectricalAppliances` | `0` | Most electrical appliances in one stack. |
| `DailyNecessities` | `0` | Most daily goods in one stack. |
| `Tool` | `0` | Most tools in one stack. |
| `Pet` | `0` | Most pet items in one stack. |
| `Pack` | `0` | Most pack items in one stack. |
| `Material` | `0` | Most materials in one stack. |
| `Fertilizer` | `0` | Most fertilizer items in one stack. |
| `Trap` | `0` | Most traps in one stack. |
| `Fuel` | `0` | Most fuel items in one stack. |
| `BagFurniture` | `0` | Most furniture items (carried in the bag) in one stack. |
| `Flower` | `0` | Most flowers in one stack. |
| `Throwable` | `0` | Most throwables in one stack. |
| `RobotModule` | `0` | Most robot modules in one stack. |
| `StoryItem` | `0` | Most story items in one stack. |
| `Collectible` | `0` | Most collectibles in one stack. |
| `Record` | `0` | Most records in one stack. |
| `Renovation` | `0` | Most renovation items in one stack. |
| `TaskItem` | `0` | Most task items in one stack. |

Each takes a whole number from 0 to 999; a bigger number counts as 999 and a negative one as 0. `0`, or any number at or below the game's own limit, keeps the game's limit: a setting only ever raises a limit, it never lowers one. Items with portions or several uses always keep the game's limit.

`BepInEx\stacking.log` lists every category at the first start, with how many items it has and the game's own limit, so you can see what a number would change before you pick it.

For items the game never stacked (most food, for example), only items with the same freshness left (within an hour) and the same uses share a stack. Seeds of one kind always share a stack, since they never spoil.

## Cooking

| Setting | Default | What it does |
| --- | --- | --- |
| `CountWholeStacks` | `true` | A cooking bench counts every item in a stack as an ingredient, as it counts loose items, and a finished cook uses up every item its dishes took. `false` counts a stack as one ingredient, as without this mod. |

A cook that was started with this on and finishes after you turn it off is settled the game's way: that one cook takes only one item from each stack, so its extra dishes come free.

## Splitting

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `true` | Drop a stack with Shift held to move half of it, Ctrl to move one, Ctrl+Shift to choose how many. Right-click a stack with the same keys to split it onto the nearest free spot in its own bag. `false` makes a drag move the whole stack and a right-click do what the game does, as without this mod. |

Turning splitting off does not change the station rule: a stack moved into a cooking or crafting station still puts in one item.
