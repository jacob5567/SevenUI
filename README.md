# SevenUI

A client-side Risk of Rain 2 mod that restyles the HUD with the Barlow font and flat translucent panels with spaced capital headers. It keeps the game's layout and its own HUD logic, so other HUD mods keep working.

## What it changes

- **Flat HUD:** the game turns the HUD's corners slightly inward, so they lean toward the middle of the screen. SevenUI levels them so the whole HUD faces the screen squarely.
- **Text:** HUD text uses Barlow instead of the game's Bombardier font, and numbers (the run timer, your level, item and buff stacks, and skill and equipment cooldowns) and the boss's name use Barlow Condensed. This covers new HUD text too: item and buff icons, notifications, ally cards, the scoreboard, and panels added by other mods.
- **Run info:** the timer, stage, level and difficulty icon share one flat translucent panel. The scrolling difficulty segments are replaced by a thin bar that fills as the current difficulty progresses, with the difficulty's name beside it in spaced capitals.
- **Health and level:** the health, shield, barrier and experience bars are flat, on a dark trough, with the experience bar thin and in the accent color. The health numbers are text instead of sprite digits, with the current health in bold and the maximum in grey. The level sits under a small "LV" header, in the accent color. Bars whose pattern means something, such as curse or the execute threshold, keep the game's look.
- **Prompts and notifications:** the interaction prompt ("Open Chest") and pickup notifications sit on flat translucent panels, with the notification's title in bold and its description in grey.
- **Ally cards and buffs:** ally cards sit on flat panels with flat health bars like yours. Buff stack counts, and LookingGlass's buff timers, are bold Barlow Condensed.
- **Skills and equipment:** the slots are flat and dark. A recharging skill gets a dark cover over the top of its icon that shrinks as it recharges, in place of the game's radial sweep, and the game's white ready outline is gone (the icon still turns grey while a skill isn't ready). The equipment keeps a thin orange ready outline. Cooldowns and stocks use Barlow Condensed, and the key labels are small grey capitals without their dark plates.
- **Top:** money, void coins and lunar coins each sit on their own flat panel, without the cluster's frame, and the item bar loses its frame. Each item sits on a dark slot with its tier's color along the bottom, and stack counts are Barlow Condensed numbers instead of sprite digits. The boss bar is flat in a muted red, with the boss's name in Barlow Condensed capitals.
- **Panels:** the panels under the run info (artifacts, objectives, and panels other mods add there) are flat and translucent, with a small gap between them. The objective panel's title sits in a header strip in spaced capitals.

Not yet restyled: the buff icons themselves, the chat, the scoreboard, tooltips, and menus outside the HUD.

## LookingGlass

[LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) works alongside SevenUI. Its stats panel, buff timers and item counters are added to the game's own HUD objects, so they pick up the restyle like the rest of the HUD. The stats panel is a copy of the objective panel, so it gets the same flat panel, but keeps LookingGlass's own text layout rather than a header strip.

## Multiplayer

The mod only changes how your own HUD looks. It never changes gameplay and never sends anything over the network, so it works when you join a host who doesn't have it installed.

## Configuration

Edit `BepInEx/config/sevenstack.SevenUI.cfg`, or use the in-game settings menu if [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed (optional).

| Setting | Default | Description |
|---|---|---|
| `BarlowFont` | true | Draw HUD text in Barlow instead of the game's font. Languages the game draws in Noto Sans keep it. |
| `FlatPanels` | true | Draw the HUD flat: facing the screen squarely instead of leaning in, with flat translucent panels, flat bars and slots, and the health numbers as text. Off brings back the game's look. Everything under "What it changes" except the fonts. |
| `DebugLogging` | false | Log extra detail, and enable the dump key. |
| `DebugAlwaysShow` | false | Show the mod's visuals even when they wouldn't normally appear, for testing. |
| `DumpKey` | F8 | With `DebugLogging` on, press to log each HUD's fonts and what the mod restyled. |

## Notes

- Barlow has Latin letters only. Languages the game draws in Noto Sans (Russian, Chinese, Japanese, Korean and others) keep the game's font. Letters Barlow lacks in other languages are drawn in the game's font.
- Barlow is baked into a font atlas when the first HUD appears, which takes a moment once per game launch.

## Building

Requires the .NET 8 SDK, a local Risk of Rain 2 install, and [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) in the r2modman profile (it is only referenced for the settings menu). `dotnet build -c Release` builds the plugin and copies it into the r2modman Default profile; add `-p:CopyToProfile=false` to skip the copy. `./build.sh` also writes the Thunderstore zip to `dist/`. The default game and profile paths are in `Directory.Build.props`; override them on the command line or in a `Directory.Build.props.user` file.

`tests/` holds in-game tests, run with [ModTestRunner](https://github.com/jacob5567/ModTestRunner). ModTestRunner builds them into a separate dev-only plugin, which is never packaged: clone it and run `./build-tests.sh <path to this repo>` there. Then start a run with DebugToolkit installed, and enter `mod_test --mod SevenUI` in the console.

The version lives in `thunderstore/manifest.json`. To release, bump it, run `./build.sh`, and publish a GitHub release tagged `v<version>` with the zip attached. The publish workflow then uploads that zip to Thunderstore.

## License

[GPL-3.0](https://github.com/jacob5567/SevenUI/blob/main/LICENSE). The Barlow fonts in `fonts/` are by The Barlow Project Authors, under the [SIL Open Font License 1.1](https://github.com/jacob5567/SevenUI/blob/main/fonts/OFL.txt).
