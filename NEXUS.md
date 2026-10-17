Name: Stacking
Summary: Bigger stacks per item category that keep fresh food apart from old food, cooking and crafting that count every item in a stack, no whole stacks in cooking stations, and splitting stacks with Shift, Ctrl or a how-many panel. 按物品类别提高堆叠上限，新鲜食物不会和旧食物混堆；烹饪和制作按堆叠里的每一件计数；烹饪和制作家具里不放整堆；按住 Shift、Ctrl 或用数量面板拆分堆叠。

Description (BBCode):
Most seeds and food take one bag slot each, so storage fills up fast. Stacking raises the stack limit per item category without letting a fresh meal merge into a rotting one, and lets you split stacks with a key held.

[size=5]Main features[/size]
[list]
[*][b]Stack limits per category[/b]: materials and seeds stack to 99 and food to 20 out of the box, with smaller raises for meds, fuel, fertilizer and other everyday items. Every category can be changed in the config, up to 999.[/*]
[*][b]Fresh food stays apart[/b]: food only stacks with food of the same freshness, so nothing old sneaks into a fresh stack.[/*]
[*][b]Seeds stack together[/b]: seeds of one kind usually join the same stack.[/*]
[*][b]Cooking counts every item[/b]: a stack of three strawberries makes three smoothies, the same as three loose ones.[/*]
[*][b]No stacks in stations[/b]: moving a stack into a cooking or crafting station puts in one item, and the rest stays where it was.[/*]
[*][b]The crafting table counts every item[/b]: a recipe that needs two glass can be made from a stack of eight, and the fill button and batch crafting take single items off the stack.[/*]
[*][b]Split by drag[/b]: hold Shift to move half a stack, Ctrl to move one, or Ctrl+Shift to choose how many.[/*]
[*][b]Split by right-click[/b]: the same keys with a right-click split the part off onto the nearest free spot.[/*]
[*][b]Ctrl-click opens no item details[/b]: in any window, from the bag to the merchant's trade window. In the bag, Ctrl-click still uses an item at once where the game allows it.[/*]
[*][b]Dishes and items with uses keep their limit[/b]: dishes never stack, since each one holds its own servings, and tools with uses are left alone.[/*]
[/list]

[size=5]Hotkeys[/size]
[list]
[*][b]Shift + drop[/b]: move half the stack.[/*]
[*][b]Ctrl + drop[/b]: move one.[/*]
[*][b]Ctrl + Shift + drop[/b]: a small panel asks how many.[/*]
[*][b]Shift, Ctrl or Ctrl + Shift + right-click[/b]: split onto the nearest free spot. With two bags open, Ctrl and Ctrl + Shift right-click stay the game's own "send every item of this kind".[/*]
[*][b]Ctrl + click[/b]: never opens the item details. In the bag it is still the game's quick use.[/*]
[/list]

The mod adds no data of its own to your save and does not edit game files, but the stacks and moves it makes are real items in your bags and storages. You can remove it at any time; stacks already bigger than the game's limit stay as they are.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*][b]Back up your save before trying it for the first time.[/b] The mod moves, splits and merges real items, and moving items around is finicky.[/*]
[*]Version 0.4.3 has not been played through in the game yet.[/*]
[*]Dishes that already share a stack from an earlier version stay in that stack.[/*]
[*]The merchant's trade window offers whole stacks, so split a stack in your bag first.[/*]
[*]Do not use it together with BaseButler. Remove BaseButler first.[/*]
[*]Dropping a stack on the floor moves the whole stack, whatever you hold.[/*]
[*]Compost bins, material racks and robot molds refuse a split. Split in your bag first, then drag the part you want.[/*]
[*]Two items up to an hour apart in freshness can share a stack.[/*]
[/list]
If an item goes missing, doubles or lands somewhere odd, please tell me (see Feedback below).

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.18902. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Not together with BaseButler.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Put [font=Courier New]Stacking.dll[/font] in the BepInEx&#92;plugins folder inside the game folder. Full path example:
[code]C:&#92;Program Files (x86)&#92;Steam&#92;steamapps&#92;common&#92;Survival Log&#92;BepInEx&#92;plugins&#92;Stacking.dll[/code][/*]
[*]If you use BaseButler, delete its DLL from the same folder.[/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]Stacking.dll[/font] from the [font=Courier New]BepInEx&#92;plugins[/font] folder. Stacks already bigger than the game's limit stay in your save as they are.

[size=5]Configuration[/size]
To change the stack limit of any item category, or turn off the cooking count or splitting, edit [font=Courier New]BepInEx&#92;config&#92;com.dev.survivallog.stacking.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-Stacking/blob/main/CONFIG.md]CONFIG.md[/url] for every setting; the cfg file explains each setting in English and Chinese). Changes apply at the next game start.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx&#92;LogOutput.log[/font] and [font=Courier New]BepInEx&#92;stacking.log[/font].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-Stacking]github.com/AnExiledDev/SurvivalLog-Stacking[/url]

[line]
[size=5]简体中文[/size]

大多数种子和食物各占一个背包格子，储物空间很快就满了。Stacking 按物品类别提高堆叠上限，但不会让新鲜的饭菜和快要腐坏的混在一起，还可以按住按键拆分堆叠。

[size=5]主要功能[/size]
[list]
[*][b]按类别设置堆叠上限[/b]：默认材料和种子可堆叠 99 个，食物 20 个；药品、燃料、肥料和其他日用品的上限也有小幅提高。每个类别都可以在配置中修改，最高 999。[/*]
[*][b]新鲜食物单独堆放[/b]：食物只和新鲜程度相同的食物叠在一起，旧食物不会混进新鲜的一堆。[/*]
[*][b]种子叠在一起[/b]：同一种种子通常会叠进同一堆。[/*]
[*][b]烹饪按每一件计数[/b]：一堆三个草莓能做三杯奶昔，和三个散放的草莓一样。[/*]
[*][b]家具里不放整堆[/b]：把一堆物品移进烹饪或制作家具时只放进一件，其余留在原处。[/*]
[*][b]工作台按每一件计数[/b]：需要两块玻璃的配方可以用一堆八块的玻璃来做，一键填充和批量制作会从堆叠里逐件取用。[/*]
[*][b]拖拽拆分[/b]：按住 Shift 移动半堆，按住 Ctrl 移动 1 件，按住 Ctrl+Shift 自选数量。[/*]
[*][b]右键拆分[/b]：按住同样的键右键点击，会把拆出的部分放到最近的空位。[/*]
[*][b]Ctrl+点击不打开物品详情[/b]：在任何界面都是如此，从背包到商人的交易界面。在背包里，游戏允许时 Ctrl+点击仍会直接使用物品。[/*]
[*][b]菜品和有使用次数的物品保持原上限[/b]：每份菜品都有自己的份数，所以菜品从不堆叠；有使用次数的工具也保持不变。[/*]
[/list]

[size=5]快捷键[/size]
[list]
[*][b]Shift + 放下[/b]：移动半堆。[/*]
[*][b]Ctrl + 放下[/b]：移动 1 件。[/*]
[*][b]Ctrl + Shift + 放下[/b]：弹出一个小面板，选择移动数量。[/*]
[*][b]Shift、Ctrl 或 Ctrl + Shift + 右键[/b]：拆分到最近的空位。同时打开两个背包时，Ctrl 和 Ctrl + Shift + 右键仍是游戏自己的「发送所有同类物品」。[/*]
[*][b]Ctrl + 点击[/b]：从不打开物品详情。在背包里仍是游戏的快速使用。[/*]
[/list]

模组不会往存档里写入它自己的数据，也不会修改游戏文件，但它做出的堆叠和移动都是你背包和储物空间里的真实物品。你可以随时移除它；已经超过游戏上限的堆叠会保持原样。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*][b]第一次使用前请备份存档。[/b]模组会移动、拆分和合并真实的物品，而移动物品这件事比较难处理。[/*]
[*]0.4.3 版还没有在游戏中完整试玩过。[/*]
[*]以前版本里已经叠在一起的菜品会保持原样。[/*]
[*]商人的交易界面只接受整堆，请先在背包里拆分。[/*]
[*]不要和 BaseButler 一起使用。请先移除 BaseButler。[/*]
[*]把一堆物品丢到地上时，无论按住什么键都会移动整堆。[/*]
[*]堆肥箱、材料架和机器人模具不接受拆分。请先在背包里拆分，再把需要的部分拖过去。[/*]
[*]新鲜程度相差不到一小时的两件物品可能叠在同一堆。[/*]
[/list]
如果有物品丢失、重复或出现在奇怪的地方，请告诉我（见下方反馈）。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.18902。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]把 [font=Courier New]Stacking.dll[/font] 放进游戏目录下的 BepInEx&#92;plugins 文件夹。完整路径示例：
[code]C:&#92;Program Files (x86)&#92;Steam&#92;steamapps&#92;common&#92;Survival Log&#92;BepInEx&#92;plugins&#92;Stacking.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx&#92;plugins[/font] 文件夹中删除 [font=Courier New]Stacking.dll[/font]。

[size=5]配置[/size]
如需修改各类别的堆叠上限，或关闭整堆计数和拆分，请在游戏关闭时编辑 [font=Courier New]BepInEx&#92;config&#92;com.dev.survivallog.stacking.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-Stacking/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。修改会在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx&#92;LogOutput.log[/font] 和 [font=Courier New]BepInEx&#92;stacking.log[/font]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-Stacking]github.com/AnExiledDev/SurvivalLog-Stacking[/url]
