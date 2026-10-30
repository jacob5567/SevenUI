# SevenUI

A client-side Risk of Rain 2 mod that restyles the HUD with the Barlow font and flat translucent panels with spaced capital headers. It keeps the game's layout and its own HUD logic, so other HUD mods keep working.

## What it changes

- **Damage numbers:** the floating damage numbers use JetBrains Mono, a monospace font, in place of the game's pixel digits, with a thinner version of the game's grey outline and dark edge, and its damage colors. They keep the font's narrower shape and are spaced to match.
- **Flat HUD:** the game turns the HUD's corners slightly inward, so they lean toward the middle of the screen. SevenUI levels them so the whole HUD faces the screen squarely.
- **Text:** HUD text uses Barlow instead of the game's Bombardier font, and numbers (the run timer, your level, item and buff stacks, and skill and equipment cooldowns), the dollar sign and the boss's name use Barlow Condensed. This covers new HUD text too: item and buff icons, notifications, ally cards, the scoreboard, and panels added by other mods.
- **Run info:** the timer, the run's difficulty and the stage and level share one flat translucent panel, wider than the game's. The timer is at the left; at the right are the difficulty's name in the accent color, in place of its icon, and the stage and monster level on one line under it. The scrolling difficulty segments are replaced by a thin bar that fills as the current segment progresses, with the segment's name beside it in spaced capitals. The timer reads minutes and seconds, "24:51", without the hundredths, and the panel is the design's height, with the panels under it a small gap below.
- **Health and level:** laid out as on SevenUI's design. The level is at the left, a large accent number under a small "LV" header. Beside it are the health numbers ("1,240 / 1,520", the current health in bold) with the health regeneration ("+12.4/s") at the right, the health bar under them, and a thin accent experience bar under that. The bars are flat on a dark trough: health green and shield blue, with the health red once it is under 25%, when the numbers turn pink too. The barrier is a pale gold strip along the top of the health bar, so the health and shield under it still show, and maximum health a curse has taken away is striped. Bars whose pattern means something else, such as the execute threshold, keep the game's look.
- **Scoreboard and chat:** the scoreboard (Tab) is a dark flat board with grey capital headers, faint flat rows, and flat slots for portraits, key items and equipment; its items look like the item bar's. On a gamepad the focused part still lights up, as a thin outline. The chat's input field is a flat dark panel.
- **Tooltips:** tooltips of things in the HUD (items, skills, equipment, including LookingGlass's item stats) use Barlow, with a bold title and without the blue outline. Tooltips in menus are left as they are.
- **Prompts and notifications:** the interaction prompt ("Open Chest") sits on a flat translucent panel fitted to its text, and pickup notifications on flat translucent panels, with the notification's title in bold and its description in grey.
- **Ally cards and buffs:** ally cards sit on flat panels with flat health bars like yours. Each buff sits on a dark square like an item, with a strip along the bottom: green for a buff, red for a debuff, gold for a cooldown. Buffs sit in rows above the level and bars, wrapping onto another row above once they would run past the bars' width. A buff's stack count is a bold Barlow Condensed number in its square's top-right corner, shown once it has more than one stack, and LookingGlass's buff timers sit under the squares.
- **Skills and equipment:** the slots are flat and dark, at the design's size and spacing, and the equipment and key item slots are the same size as the skills, with a thin divider between the skills and the equipment. A skill with charges has a thin accent bar along its bottom that fills as the next charge recharges. A recharging skill gets a dark cover over the top of its icon that shrinks as it recharges, in place of the game's radial sweep, and the game's white ready outline is gone (the icon still turns grey while a skill isn't ready). The equipment keeps a thin orange ready outline. Cooldowns and stocks use Barlow Condensed, and the key labels (including the inventory and sprint keys) are small grey capitals without their dark plates.
- **Top:** money, void coins and lunar coins sit side by side, each on its own flat panel fitted to its amount, without the cluster's frame. Money is gold and lunar coins pale blue. The item bar loses its frame and, like the boss bar under it, takes the middle 47% of the screen's width, so a full row stays clear of the run info. Each item sits on a dark slot with its tier's color along the bottom, and stack counts are Barlow Condensed numbers instead of sprite digits. The boss bar is flat in a muted red. Above it, one row holds the boss's name in Barlow Condensed capitals, its subtitle and its health ("2,730 / 2,730"). Health the boss just lost stays drawn in dark red after the fill for a moment, then drains away.
- **Panels:** the panels under the run info (artifacts, objectives, and panels other mods add there) are flat and translucent, with a small gap between them. The objective panel's title sits in a header strip in spaced capitals, and each objective is left-aligned after a small flat square outline, which becomes the game's check or cross once the objective is done or failed. The teleporter's charge shows its percentage at the right in the accent color, with a thin bar under the objective that fills as it charges.

Not yet restyled: the buff icons themselves, and menus outside the HUD.

## LookingGlass

[LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) works alongside SevenUI. Its stats panel, buff timers and item counters are added to the game's own HUD objects, so they pick up the restyle like the rest of the HUD. The stats panel is a copy of the objective panel, so it gets the same flat panel, but keeps LookingGlass's own text layout rather than a header strip.

## Multiplayer

The mod only changes how your own HUD looks. It never changes gameplay and never sends anything over the network, so it works when you join a host who doesn't have it installed.

## Configuration

Edit `BepInEx/config/sevenstack.SevenUI.cfg`, or use the in-game settings menu if [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed (optional).

| Setting | Default | Description |
|---|---|---|
| `BarlowFont` | true | Draw HUD text in Barlow instead of the game's font. Languages the game draws in Noto Sans keep it. |
| `MonospaceDamageNumbers` | true | Draw the floating damage numbers in JetBrains Mono, a monospace font, instead of the game's pixel digits. |
| `FlatPanels` | true | Draw the HUD flat: facing the screen squarely instead of leaning in, with flat translucent panels, flat bars and slots, and the health numbers as text. Off brings back the game's look. Everything under "What it changes" except the fonts. |
| `TightMargins` | true | Draw the HUD's corners near the screen's edges, as on SevenUI's design: 2.6% of the screen's height in (2.2% at the top). Off keeps the game's margins, 5% of the screen. Only with `FlatPanels` on. |
| `DebugLogging` | false | Log extra detail, and enable the dump key. |
| `DebugAlwaysShow` | false | Show the mod's visuals even when they wouldn't normally appear, for testing. |
| `DumpKey` | F8 | With `DebugLogging` on, press to log each HUD's fonts and what the mod restyled. |

## Notes

- Barlow has Latin letters only. Languages the game draws in Noto Sans (Russian, Chinese, Japanese, Korean and others) keep the game's font. Letters Barlow lacks in other languages are drawn in the game's font.
- Barlow is baked into a font atlas when the first HUD appears, which takes a moment once per game launch.

## Building

Requires the .NET 8 SDK, a local Risk of Rain 2 install, and [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) in the r2modman profile (it is only referenced for the settings menu). `dotnet build -c Release` builds the plugin and copies it into the r2modman Default profile; add `-p:CopyToProfile=false` to skip the copy. `./build.sh` also writes the Thunderstore zip to `dist/`. The default game and profile paths are in `Directory.Build.props`; override them on the command line or in a `Directory.Build.props.user` file.

`tests/` holds in-game tests, run with [ModTestRunner](https://github.com/jacob5567/ModTestRunner). ModTestRunner builds them into a separate dev-only plugin, which is never packaged: clone it and run `./build-tests.sh <path to this repo>` there. Then start a run with DebugToolkit installed, and enter `mod_test --mod SevenUI` in the console. `mod_test --mod SevenUI --suite screenshots` instead sets up each restyled part of the HUD (items, the scoreboard, shield and barrier, cooldowns, the N'tormat's pages beside the key item, the interaction prompt, a pickup notification, ally cards, damage numbers, the boss bar, and the HUD of each survivor with elements of its own: Railgunner, Void Fiend, Seeker, Drone Tech, Drifter and Acolyte) and saves a screenshot of each in SevenUI's look and the game's, in `BepInEx/ModTestRunner/Screenshots/`. It changes the player's survivor and starts the teleporter, so run it on its own. `mod_test --mod SevenUI --test <name>` runs one scenario, such as `boss_bar`; `mod_test --mod SevenUI list` names the checks, and `mod_test --mod SevenUI --suite screenshots list` the screenshots. Survivors and the N'tormat are skipped without their expansion.

The version lives in `thunderstore/manifest.json`. To release, bump it, run `./build.sh`, and publish a GitHub release tagged `v<version>` with the zip attached. The publish workflow then uploads that zip to Thunderstore.

## License

[GPL-3.0](https://github.com/jacob5567/SevenUI/blob/main/LICENSE). The Barlow fonts in `fonts/` are by The Barlow Project Authors, under the [SIL Open Font License 1.1](https://github.com/jacob5567/SevenUI/blob/main/fonts/OFL.txt). The damage numbers are drawn from JetBrains Mono, by The JetBrains Mono Project Authors, under the [SIL Open Font License 1.1](https://github.com/jacob5567/SevenUI/blob/main/fonts/OFL-JetBrainsMono.txt); `tools/make_damage_numbers.py` renders them.
