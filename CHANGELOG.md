## 1.8.0

- The scoreboard shows each player's drones and other allies: under their name, the portraits of up to four kinds of ally and how many they have ("7 allies"). Clicking that line opens the player's allies in a panel under the board, with a tile per kind showing its count and each ally's health. Clicking it again, or closing the scoreboard, closes the panel. The new `ScoreboardAllies` setting turns this off.
- Pointing at a drone's tile shows what the drone does, as its notification does when you buy one, or its skill description with LookingGlass installed. As Operator with ADMIN-OVERRIDE, the drone's command follows in green.

## 1.7.0

- The scoreboard's inspect panel, shown for the pickup, chest, shrine or drone beside you, is a flat panel centered under the board at 60% of its width: a thin strip in the item's tier color, the icon on a dark slot, and the name in bold over the description.
- With LookingGlass's full descriptions on, the inspect panel shows an item's stats in a column at its right.
- The Drone Scrapper's panel (Alloyed Collective) matches the other item pickers: a grid of drones that scrolls when long, the hovered drone's name and description, and the scrap it gives.
- With LookingGlass's full descriptions on, item stats count the stack you'd end up with. Pickers that take the item (the Lemurian egg, the rebirth shrine, the Solus vendor) show one less stack, pickers that give it show one more, and an item you click on the scoreboard shows the stack you hold.
- Item picker options show how many you hold once, without LookingGlass's own "x2" beside SevenUI's count. For an item you hold none of, the count shows the held items tied to it by void corruption, in their color.

## 1.6.0

- The boss bar is drawn as your health bar is: health the boss just lost trails behind in dark red, health just healed shows in a lighter red, and its shield, void shield, barrier, Lunar Ruin and curse show as on your own bar. A thin line marks where the boss would be executed, while it is frozen or, for an elite boss, at your Old Guillotine threshold.
- The moon's escape, Winnowing trial and Champ Rush countdowns are flat panels: the event's name on a header strip, the time left in Barlow Condensed and a bar that drains with it, turning red in the last 10 seconds.
- Confirmation and message dialogs, such as the quit confirmation, get a flat panel with the pause menu's buttons, in the menus too.
- Bandit's Desperado stacks show above the special slot as the buff's icon and the count.
- With MusicDisplay installed, the playing track shows in a "Now playing" panel under the objectives.
- A Lemurian egg used with no items to give says "No items to offer" instead of opening an empty panel.

## 1.5.0

- Other players' ally cards stay full size at the top, each followed by that player's allies, indented under them as the game does, with a "+N more" row of their own when the cards are crowded. Your own allies come after every player's.
- Drones of different upgrade tiers no longer share an ally card. A new Gunner Drone keeps its own card beside an upgraded one.
- A shared ally card no longer shows the dead X while its allies are alive, after one of them dies, another drone dies or is scrapped, or a Drone Combiner upgrades one.
- On compact ally rows, the barrier fills the bar from its left edge.
- Operator's drone queue and Drifter's bag display keep the game's size and place over the larger skill row.
- Operator's drone notifications keep their command line within the panel, in the game's green.
- On the scoreboard, the equipment and item highlight outlines are the same thickness on every side, a tab's items are centered, and in a solo run the item panel no longer lists you as the only holder.

## 1.4.0

- Eleven new settings, each in the Panels section and in Risk Of Options, so you can keep parts of the game's look while the rest is restyled:
  - `EquipmentDeck`: the Functional Coupler's deck of equipment above the equipment slot.
  - `RestyleItemPickers`: the restyled item pickers, such as Command and Void Potentials.
  - `CompactAllyCards`: ally cards shrinking to one-line rows and folding into "+N more".
  - `GroupAllyCards`: allies of one kind sharing a card.
  - `LevelHud`: the HUD's corners drawn level, instead of with the game's inward lean.
  - `ScoreboardDetailPanel`: the panel docked on the scoreboard, instead of the game's tooltip.
  - `ScoreboardTabs`: scoreboard rows collapsing to tabs; off, every row stays and the items shrink to fit.
  - `RestylePauseMenu`: the restyled pause menu.
  - `RestyleTooltips`: the restyled tooltips.
  - `TimerHundredths` (off by default): the run timer's hundredths of a second.
  - `ShowHealthRegen`: the regeneration text, such as "+12.4/s", beside the health numbers.
- Turning `FlatPanels` off from the pause menu now restores the pause menu too, and its labels keep Barlow.
- Turning `MonospaceDamageNumbers` off in the main menu takes effect at once, instead of at the next run.
- A scoreboard row that couldn't be restyled when it appeared is restyled once it can be.
- Every ally card lines up on the same left edge, compact names are all one size, and compact portraits stay small after a setting changes.
- In a solo run, the scoreboard no longer shows how many players hold each item.
- The `DebugAlwaysShow` setting, which did nothing, is gone.
- Less work each frame in the HUD.

## 1.3.0

- The scoreboard keeps every item at full size. Each player's row reads left to right: portrait, name and item counts by tier, key item, equipment, then their items, sorted by tier and wrapping under themselves. With several players and too many items to fit, the rows collapse: your row, or the one you point at, shows as an outlined tab with all its items, and the others as plain rows beside it.
- Pointing at an item, equipment or key item in the scoreboard describes it in a panel docked on the board, with every player who holds it, instead of a tooltip over the items.
- With a Functional Coupler, the equipment you can switch to shows above the equipment slot, where it was off the screen: the next one with its cooldown, a small arrow into the slot and your Interact and Equipment keys, and the ones after it fading behind it. Switching plays a short animation of the equipment trading places. MUL-T's second equipment shows the same way, without the keys.
- Ally cards stay clear of the chat. Allies of one kind still share a card; when the cards would reach the chat, the cards of single allies become one-line rows, and if that is still too tall, the last cards fold into a "+N more" row with an icon for each kind. Cards no longer overlap with many allies, and they hide while the scoreboard is open instead of drawing over it.
- Item transformation notifications, including corruptions and Benthic Bloom, show both items at one size with a thin arrow between them, shaded from the old item's tier color to the new one's.
- The Winnowing trials' notification strip is the lime of their icon.
- The thin outlines on the pause menu's buttons, objective checkboxes, the chat's input field and item pickers are the same width on every side.

## 1.2.0

- The pause menu is restyled: "PAUSED" over a line with the run's time, stage and difficulty, and the buttons as outlined buttons in one centered stack.
- Allies of one kind share a card, such as "Gunner Drone ×8", with a bar segment for each ally's health, so a crowd of drones no longer covers the chat.
- Item picker cards show the item centered on a band across the card's top.
- Void Fiend's corruption percentage keeps the game's font.
- The Karma row reads the run's synced karma, so a player who joins mid-run sees the right level instead of none.

## 1.1.1

- An item picker's grid has as many items to a row as the game's own grid, including LookingGlass's larger Command window. Large Command groups no longer run their last row into the item's details.
- Item picker cards are wider, so LookingGlass's "With this item, you will have:" fits on one line.
- In Command, items whose stats LookingGlass can't show (such as Cautious Slug and Tougher Times) no longer end with an empty "With this item, you will have:".

## 1.1.0

- The same as 1.0.1, numbered as a minor release for its new artifacts row.

## 1.0.1

- Fixed the frame rate falling steadily after a few minutes in a run.
- The run's artifacts show as a row in the run info, under the difficulty bar and Karma, in place of the separate artifact panel. The icons keep their tooltips and shrink to stay on one line when many artifacts are on.
- The Karma icon's dark backdrop no longer shows beside the timer.
- The scoreboard stays clear of the item bar when the bar has two rows of items.
- The line between an item picker's grid and the item's details is easier to see.

## 1.0.0

- First release.
- HUD text uses the Barlow font, centered on its capitals.
- Damage numbers use JetBrains Mono, a monospace font.
- The HUD faces the screen squarely instead of its corners leaning inward.
- The run timer, the level, and skill and equipment cooldowns use Barlow Condensed.
- The health and experience bars are flat, the barrier is a strip along the top of the health bar, and the health numbers are text instead of sprite digits.
- Money and the boss bar are flat, and the item bar and money lose their frames. Lunar coins have a flat coin icon, also in prompts that cost them.
- The interaction prompt and pickup notifications are flat panels. The prompt's keys are keycaps, and a strip along its top takes the cost's color; a notification's strip takes the color of what was picked up.
- The scoreboard is flat, and the chat's input field is outlined with an accent strip at its left.
- Tooltips of HUD items, skills and equipment are flat, with the item's tier color along the top, its stack count beside its name, and LookingGlass's item stats in their own section.
- Item pickers (Command, the potentials and the like) are flat: a few options as cards with their descriptions, more as a grid of large slots with the hovered item's description under it.
- Ally cards are flat panels with flat health bars, and buff counts are bold.
- Items sit on dark slots with their tier's color along the bottom, and stack counts are text.
- The skill and equipment slots are flat, with a top-down cooldown cover and small grey key labels. The scoreboard and sprint buttons have flat icons, and the tutorial popups over them are dark panels.
- The run info is one flat translucent panel with the timer first, with a thin bar and the difficulty's name in place of the scrolling difficulty segments. Eclipse levels read "Eclipse 3", and Karma is a row of eight segments under the bar.
- The objective, artifact and LookingGlass stats panels are flat translucent panels, and the objective panel has a header strip.
