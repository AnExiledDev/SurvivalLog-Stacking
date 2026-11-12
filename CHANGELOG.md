# Changelog

## 0.8.6

- Clearer wording in the settings and in the messages a split shows.
- Made for Survival Log 1.1.20719.

## 0.8.5

- Now needs Exiled's Toolkit 0.20.0 or newer.
- Made for Survival Log 1.1.20719.

## 0.8.4

- A new install now keeps the game's stack limits until you set your own. Every `[StackLimits]` setting defaults to `0`. Each setting suggests a number, such as 99 for materials and seeds or 20 for food, which were the old defaults. Settings you already saved are kept.
- Made for Survival Log 1.1.20719.

## 0.8.3

- Now uses Exiled's Toolkit for its shared parts, so it needs Exiled's Toolkit 0.19.0 or newer.
- Made for Survival Log 1.1.20719.

## 0.8.2

- Fixed for the 1.1.20719 game update: the cooking screen counts every item in a stack again when it works out which dishes you can make.
- Made for Survival Log 1.1.20719.

## 0.8.1

- **Warning:** needs Exiled's Toolkit 0.11.0 or newer. Update the Toolkit first.
- Stacking's short messages now show as Exiled's Toolkit toasts, so they appear beside the game's HUD and in the Notifications history.
- Made for Survival Log 1.1.20151.

## 0.8.0

- New: `UiScale` under Interface sets the size of Stacking's windows by itself, from 75% to 200%, separate from Exiled's Toolkit's UI size. At 0, the default, they follow the Toolkit's size. Needs Exiled's Toolkit 0.11.0 or newer.
- Made for Survival Log 1.1.19611.

## 0.7.11

- Fixed: a stutter the first time in a session that you pick a recipe at the crafting table.
- Fixed: shorter stutters when the crafting table checks or fills recipes from storages that hold many items.
- Made for Survival Log 1.1.19611.

## 0.7.10

- The panel that asks how many to split, and the mod's short messages, can be drawn bigger or smaller with the UI size setting of Exiled's Toolkit 0.10.0 or newer. A change shows at once.
- Made for Survival Log 1.1.19611.

## 0.7.9

- Fixed: a stutter when you pick a recipe at the crafting table and the mod takes single items off stacks for it.
- Fixed: a stutter when the mod moves items into or out of a crafting or cooking station while its screen is open.
- Fixed: short stutters while the mod writes its log file.
- Made for Survival Log 1.1.19611.

## 0.7.8

- Fixed: a stutter when the crafting table's recipe list updates while stacks of the same item sit in its storages.
- Fixed: a stutter when a save loads, while the mod sets the stack limits.
- Fixed: the mod did extra work in the game's menus every 3 seconds, even when nothing had changed.
- Made for Survival Log 1.1.19611.

## 0.7.7

- Fixed: a short stutter every 5 seconds. The mod now checks the item table only when the game loads it again or a stack limit changes.
- Made for Survival Log 1.1.19611.

## 0.7.6

- With the refrigerated backpack upgrade, harvested crops and other food that join a stack in the backpack keep their full shelf life instead of losing part of it.
- Made for Survival Log 1.1.19611.

## 0.7.5

- Fixed: at the crafting table, a recipe you can only make by taking single items off stacks shows as ready again.
- Made for Survival Log 1.1.19611.

## 0.7.4

- After a game update, Mod Manager can tell you if this mod may need an update.
- Made for Survival Log 1.1.19131.

## 0.7.3

- Fixed: in the merchant's trade window, dropping a stack on a stack of the same item did not join them, in your bag or on the drone. An offer that gains or loses items now updates to the new amount.

## 0.7.2

- A quick-move click on a stack in a material rack or a storage tab of the shredder, compressor or dye vat window now puts in one item and leaves the rest of the stack where it was.
- In the merchant's trade window, a right-click or Shift left-click on a stack on your side offers one item, from a material rack too. Another click adds one more to that offer. The highlight key still offers every stack of the kind.
- Shift, Ctrl and Ctrl+Shift drags split inside the drone and inside your bag in the trade window, and the split part lands where you dropped it.
- You can now split a stack out of a material rack or inside one. Splitting into a rack from somewhere else is still refused.
- Fixed: a split drag from the drone to your bag moved the whole stack, or did not land where it was dropped.

## 0.7.1

- You can now take part of an offer back in the trade window. Drag a stack from the drone to your bag, or right-click it on the drone, with Shift (half), Ctrl (one) or Ctrl+Shift (choose how many). Those items go back to your bag, and the rest stays offered.

## 0.7.0

- You can now offer part of a stack in the merchant's trade window. Drag a stack from your bag onto the drone, or right-click it, with Shift (half), Ctrl (one) or Ctrl+Shift (choose how many). The rest stays in your bag. An offer the merchant refuses goes back on its stack.
- Fixed: the Ctrl+Shift panel moved nothing when Split or Enter was pressed.

## 0.6.0

- Mod Manager's Mods screen links to this changelog.
- Stack limits changed on the Mod Manager's Mods screen take effect within a few seconds. A category turned down or off gives its items the game's limit again. Cooking and splitting still need a game restart.
- Mod Manager's Mods screen shows the mod's name, description, category and links.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings you change on the Mods screen take effect right away. Without it, they take effect the next time you start the game.

## 0.5.0

- Dropping a stack on a stack of the same item joins them into one stack, as far as the stack limit allows, and the rest stays where it was. Before, the game only swapped the two. Dropping a stack on anything else works as before.
- Shift, Ctrl and Ctrl+Shift drags split a stack while a crafting table, cooking bench, compressor, dye station or drone hub is open. Those windows moved the whole stack before.
- Storage Hub's Home tab takes one item per click from a stack, because other mods can now move one item off a stack.

## 0.4.9

- The crafting table's recipe list updates faster when your storages hold a lot of items.

## 0.4.8

- Reusable materials, such as Electrical Wire, Integrated Circuits, Iron Rods and sheet metal, now stack.
- Reusable items you use directly, such as dyes, the comb and dumbbells, keep the game's limit, because the game takes one from a stack each time you use one.
- Items that never spoil stack whether you found them on the map or made or bought them.

## 0.4.7

- Items that never spoil, such as diesel, gas canisters and materials, stack no matter when you picked them up. Before, only seeds did, so these items started a new stack for every pickup and Auto Organize could not merge them.

## 0.4.6

- First release.
- Bigger stacks for each item category, with fresh food kept apart from old food.
- Cooking and crafting count every item in a stack.
- Cooking stations no longer take whole stacks.
- Split stacks with Shift, Ctrl or a panel that asks how many.
