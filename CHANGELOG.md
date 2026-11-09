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
