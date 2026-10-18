# Changelog

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
