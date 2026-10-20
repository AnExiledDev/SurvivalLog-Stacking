# Changelog

## 0.7.3

- Fixed: in the merchant's trade window, dropping a stack on a stack of the same item did not join them, in your bag or on the drone. An offer that gains or loses items is counted again.

## 0.7.2

- A quick-move click on a stack in a material rack or a storage tab of the shredder, compressor or dye vat window puts one item in, not the whole stack.
- In the merchant's trade window, a right-click or Shift left-click on a stack on your side offers one item, from a material rack too. Another click adds one more to that offer. The highlight key still offers every stack of the kind.
- Shift, Ctrl and Ctrl+Shift drags split inside the drone and inside your bag in the trade window, and the split part lands where you dropped it.
- A split out of a material rack, or inside one, works. A split into a rack from elsewhere is still refused.
- Fixed: a split drag from the drone to your bag moved the whole stack, or did not land where it was dropped.

## 0.7.1

- The trade window takes part of an offer back too. Drag a stack from the drone to your bag, or right-click it on the drone, with Shift (half), Ctrl (one) or Ctrl+Shift (choose how many). Those units go back to your bag; the rest stays offered.

## 0.7.0

- The merchant's trade window now offers part of a stack. Drag a stack from your bag onto the drone, or right-click it, with Shift (half), Ctrl (one) or Ctrl+Shift (choose how many). The rest stays in your bag. An offer the merchant refuses goes back on its stack.
- Fixed: the Ctrl+Shift panel moved nothing when Split or Enter was pressed.

## 0.6.0

- The Mods screen links this changelog.
- Stack limits changed on the Mod Manager's Mods screen take effect within a few seconds. A category turned down or off gives its items the game's own limit again. Cooking and splitting still need a game restart.
- The Mods screen shows the mod's name, description, category and links.
- Exiled's Toolkit 0.4.0 or newer is optional. With it, settings changed on the Mods screen take effect without a restart; without it, at the next game start.

## 0.5.0

Dropping a stack on a stack of the same item now joins them into one stack, as far as the stack limit allows, and the rest stays where it was. The game only ever swapped the two. A drop on anything else is the game's own move, as before.

Shift, Ctrl and Ctrl+Shift drags now split a stack while a crafting table, cooking bench, compressor, dye station or drone hub is open. Those windows moved the whole stack before.

Other mods can now move one item off a stack, so Storage Hub's Home tab takes one item per click from a stack. No setting changed.

## 0.4.9

The crafting table's recipe list updates faster when your storages hold a lot of items. The mod counted the stacks in every linked storage again for each recipe, and now it reads them once for the whole list. No setting changed.

## 0.4.8

Reusable materials, such as Electrical Wire, Integrated Circuits, Iron Rods and sheet metal, now stack. The game marks them as never used up, and the mod used to treat that like items with uses. Reusable items you use directly, such as dyes, the comb and dumbbells, keep the game's limit, because the game takes one from a stack each time you use one. Items that never spoil also stack whether you found them on the map or made or bought them; the game kept those apart only because found food spoils at its own rate.

## 0.4.7

Items that never spoil, such as diesel, gas canisters and materials, now stack no matter when you picked them up. Before, only seeds did, so these items started a new stack for every pickup and Auto Organize could not merge them.

## 0.4.6

First public release. Bigger stacks per item category that keep fresh food apart from old food, cooking and crafting that count every item in a stack, no whole stacks in cooking stations, and splitting stacks with Shift, Ctrl or a how-many panel.
