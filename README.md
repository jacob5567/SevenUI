# SevenUI

A client-side Risk of Rain 2 mod that restyles the HUD with the Barlow font and flat translucent panels with spaced capital headers. It keeps the game's layout and its own HUD logic, so other HUD mods keep working.

## What it changes

- **Text:** HUD text uses Barlow instead of the game's Bombardier font. This covers new HUD text too: item and buff icons, notifications, ally cards, the scoreboard, and panels added by other mods.
- **Panels:** the panels under the run timer (artifacts, objectives, and panels other mods add there) are flat and translucent, with a small gap between them. The objective panel's title sits in a header strip in spaced capitals.

Not yet restyled: the timer and difficulty bar, the other HUD panels and bars, health, boss health and timer digits (the game draws those from sprite digits, not text), tooltips, and menus outside the HUD.

## LookingGlass

[LookingGlass](https://thunderstore.io/package/DropPod/LookingGlass/) works alongside SevenUI. Its stats panel, buff timers and item counters are added to the game's own HUD objects, so they pick up the restyle like the rest of the HUD. The stats panel is a copy of the objective panel, so it gets the same flat panel, but keeps LookingGlass's own text layout rather than a header strip.

## Multiplayer

The mod only changes how your own HUD looks. It never changes gameplay and never sends anything over the network, so it works when you join a host who doesn't have it installed.

## Configuration

Edit `BepInEx/config/sevenstack.SevenUI.cfg`, or use the in-game settings menu if [Risk Of Options](https://thunderstore.io/package/Rune580/Risk_Of_Options/) is installed (optional).

| Setting | Default | Description |
|---|---|---|
| `BarlowFont` | true | Draw HUD text in Barlow instead of the game's font. Languages the game draws in Noto Sans keep it. |
| `FlatPanels` | true | Draw the objective, artifact and LookingGlass stats panels as flat translucent panels, with a header strip on the objective panel. |
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
