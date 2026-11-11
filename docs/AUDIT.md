# SevenUI codebase audit

Audited 2026-11-09 at commit `3dbd56e` (version 1.3.0). Covers potential bugs, code quality and readability, coding conventions, extensibility, and settings worth adding.

## Scope and method

- **Read in full:** all of `src/` (about 9,000 lines), the build files, the publish workflow, the README, the changelog and the manifest.
- **Read in part:** `tests/`. The entry points and set-up helpers were read; the 5,000 lines of checks and scenarios were only skimmed.
- **Run:** a full rebuild (`dotnet build -c Release -p:CopyToProfile=false --no-incremental`) gives 0 warnings and 0 errors. A grep of `src/` for forbidden methods, hooks and R2API finds nothing. The README settings table matches `src/PluginConfig.cs`.
- **Not run:** nothing was tested in game. Every finding below comes from reading code unless it says otherwise.

## 1. The 2026-11-09 fixes have not run in game

The installed `SevenUI.dll` and `SevenUI.Tests.dll` in the r2modman Default profile are dated 2026-11-09 01:15. These commits are later and were built with `CopyToProfile=false`, so none of them was installed:

| Commit | Time | Change |
|---|---|---|
| `f105f86` | 02:44 | Hide the scoreboard item holder counts in a solo run |
| `6f63518` | 05:19 | Align every ally card to the same left edge (workaround) |
| `f2cbe07` | 05:28 | Fix the ally cards' left edge at its cause |
| `cca6ece`, `3dbd56e` | 08:26 | New left-edge checks, and a per-manager unindent |

The 140/140 test run at 08:08 therefore tested the old mod with the old tests.

- **Cause of the staggered ally cards.** The game's `AllyCardManager.SetLayoutHorizontal` indents a card by `indentWidth` when the minion's leader also has a card. The test's Healing Drones follow Gunner Drones, so their rows sat 16 units to the right. This is from the decompiled assembly.
- **`3dbd56e` is probably unnecessary.** There is one manager per HUD, so setting `indentWidth` to 0 in `f2cbe07` likely sufficed. The per-frame `Unindent` and its dictionary (`src/AllyCardsStyler.cs:247`) are a candidate to revert once `f2cbe07` is seen working.
- **To verify:** run a plain `dotnet build -c Release` in the main checkout so the plugin and tests are installed, run `mod_test --mod SevenUI`, and look at the compact and "more" screenshots.

## 2. Potential bugs

The first three are the ones to fix first.

1. **`DebugAlwaysShow` did nothing.** Nothing in `src/` read it. Fixed in 9a26a36 by removing the option.
2. **Turning `FlatPanels` off from the pause menu leaves the pause menu styled.** `pause.Update` runs only while the panels are applied, and `RestorePanels` has no pause step (`src/SevenUIController.cs`). Needs an in-game check.
3. **A scoreboard row that could not be styled is still counted as styled.** `src/ScoreboardStyler.cs:162` adds the row to `styled` before `StyleRow`'s early return, so the row is never retried and the items board still lays it out.
4. **Turning `MonospaceDamageNumbers` off with no HUD does not take effect.** `DamageNumberFont.Update` is driven only from a HUD's `LateUpdate`, so the shared material stays swapped until the next run.
5. **Two places take the HUD from `root`.** `HudFontSwapper.GameFontText()` and `src/ScoreboardStyler.cs:332` call `root.GetComponent<HUD>()`, though the controller notes that root can differ from the HUD. `ScoreboardStyler` already has a `hud` field.
6. **`PromptPanel` captures the run info panel once, at construction.** If that panel does not exist yet, the prompt never lines up with it. The current apply order makes this safe, but nothing enforces the order.
7. **`KarmaRow.Set` lays out from `Root.rect.width` without checking that the row is active.** `ArtifactRow` guards against this case. Whether the caller always activates the row first was not traced. Needs an in-game check with Karma on from the first frame.
8. **Two calls to settle against the read-only principle.**
   - `src/ScoreboardStyler.cs:404` calls the game's `UpdateDisplay()` and `LayoutAllIcons()`. They touch local UI only, but they have side effects, and no comment marks the exception.
   - `src/EquipmentDeck.cs:165` calls `GetEquipmentDisabled()`, which was not confirmed from the assembly to be a pure read.

## 3. Performance

- **`PauseScreenStyler.RunText()`** builds a string every frame while paused, and retries its `Find` every frame if styling failed.
- **`PickerPanel.Layout()`** re-measures every option's description on each hover change. Command can have over 100 options, so the tallest height should be cached.
- **`ProtectionStripes.LateUpdate`** runs `GetComponentsInChildren` every frame on every styled health bar.
- **`ScoreboardRow`** compares the money text by reference, so it re-measures whenever the game writes a new string, even an unchanged one.
- **`StyleRecorder`** keeps entries for destroyed objects (notifications, buff icons) until `Restore`. This is bounded by the HUD's lifetime and is minor.

## 4. Code quality and readability

- **Duplicated helpers are the main issue.**
  - `NewObject`/`NewUIObject` is copied into about 17 files, with two return types.
  - `Stretch`, `AddText`/`NewText`, `NewImage`, `KeepOnOneLine` and the "flatten image" steps are repeated.
  - Embedded-resource loading is written three times.
  - The camera `pixelRect` projection is written in `HudMargins`, `TopBarsWidth`, `PromptsStyler`, `SkillRow` and `ScoreboardPlacement`.
- **HUD paths and constants are repeated.**
  - The `MainContainer/MainUIArea/SpringCanvas/...` paths appear in 8 files.
  - `OutlineEdgePixels`, the item slot size and gap, `DividerHeight` and `CanvasHeight = 1080f` are each defined more than once.
- **`StyleRecorder` has gaps that callers patch by hand.**
  - `Record(LayoutElement)` saves only heights.
  - `Record(TMP_Text)` omits wrapping, overflow and auto-size.
  - `OnRestore` does not dedupe, so every caller needs its own run-once guard. The callers checked all have one.
- **`src/PickerPanel.cs` is 864 lines** covering cards, grid, cancel button, legend and layout. It is the one file worth splitting.
- **Name shadowing in `src/SkillSlotsStyler.cs`:** the locals `extraButtons` and `row` shadow fields of the same names.
- **`BuffRows` reads TMP internals** (`m_IsTextBackingStringDirty`, `m_text`), which is fragile across TMP or LookingGlass updates.
- **Small things.**
  - Broken comment wrapping in `HudMargins`, `RunInfoStyler`, `ScoreboardStyler`, `ScoreboardRow`, `SlotFitter` and `FlatIcons`.
  - A redundant second `IsKarmaEnabled()` at `src/RunInfoStyler.cs:154`.
  - Duplicate `hovering`/`hovered` locals in `PickerButton.Update`.
  - Stray whitespace before the `Hatch` comment in `src/FlatSprite.cs`.

## 5. Conventions

The conventions in `RoR2Mods/CLAUDE.md` are mostly followed: the plugin class is thin, classes are `internal sealed`, events are unsubscribed in `OnDestroy`, there are no hooks, and the forbidden `GetNotificationQueueForMaster` is avoided with a comment. The misses:

- **One class per file:** `ItemStatsText.Parts` and `CurrencyRow.Currency` are nested classes. This is arguably fine for private data holders.
- **Static field naming:** `EquipmentIcons.loading` is lower-case, while `Averages`, `Copies` and `Tags` are PascalCase.
- **Placement:** `NoGrids` is declared mid-class in `ScoreboardDetailPane`, and `keys` in `EquipmentDeck` is not `readonly`.
- **Language:** the scoreboard's "items" word and the "+N more" label are English-only. `ScoreboardRow` documents this; `AllyMoreRow` does not.

## 6. Extensibility

- **Adding a styler means editing five places** in `SevenUIController`: `Awake`, `LateUpdate`, `ApplyPanels`, `RestorePanels` and `Describe`. The 17 stylers already share an `Apply`/`Restore`/`Update`/`Describe` shape, so an `IStyler` interface and one ordered list would cut this to one line. It would also fix bug 2.
- **A `UiFactory` static class and a `HudPaths` class** would remove most of the duplication, and make a path change after a game patch a one-file edit.
- **`Restyle()` re-runs every styler on any change in the HUD's object count.** That works because each styler is idempotent, but each new styler has to honour that contract and nothing checks it.

## 7. Settings worth adding

Today `FlatPanels` is one switch for 17 stylers, so a player who dislikes one part has to give up the whole look. The candidates below are the parts that change what the player can see or how the HUD behaves, where a preference is likely. Parts that are only a matter of look (flat slots, the cooldown cover, the lunar coin) are left under `FlatPanels`.

All would default to true, sit in the `Panels` section, and apply only with `FlatPanels` on. The effort column is an estimate from reading the code.

### Recommended

| Setting | What off does | Why a player would want it | Effort |
|---|---|---|---|
| `LevelHud` | Keeps the game's inward lean of the HUD's corners, with everything else flat. | The lean is a matter of taste, and the README names it as its own change. | Low: `TiltFlattener` is its own styler. Sizes measured through the camera (`SkillRow`, `HudMargins`) need an in-game check with the tilt on. |
| `GroupAllyCards` | One card per ally, as the game has it. | Grouping hides each ally's name and replaces its health bar with a segment. | Low to medium: the grouping is in `AllyCardsStyler` and `AllyKindParts`. |
| `CompactAllyCards` | Cards keep full size and are never folded into "+N more". | Folding hides allies; some players would rather the cards overlap the chat. | Low: one branch in `AllyCardsStyler`'s fitting. |
| `ScoreboardDetailPanel` | The game's tooltip over the pointed-at item, instead of the docked panel. | The panel widens the board and moves the description away from the pointer. | Medium: `ScoreboardDetailPane` is wired into the board's layout and width. |
| `ScoreboardTabs` | Rows never collapse to tabs; items shrink to fit instead. | Collapsing hides every other player's items until pointed at. | Medium: `ScoreboardItemsBoard` needs a shrink path, which the game's own layout may already give. |
| `EquipmentDeck` | No deck above the equipment slot with a Functional Coupler. | It adds a new element over the skill row, which can collide with other HUD mods there. | Low: `EquipmentDeck` is created in one place. |
| `RestyleItemPickers` | Pickers keep the game's look. | Pickers are where other mods are most likely to conflict (LookingGlass's Command window, picker mods). | Low: `PickerStyler` is its own styler. |

### Worth considering

| Setting | What off does | Note |
|---|---|---|
| `TimerHundredths` (default false) | On shows the run timer's hundredths, which `TightTimerFormat` removes. | The only place the mod shows less than the game does. Players timing runs may want it. |
| `RestylePauseMenu` | The pause menu keeps the game's look. | The pause menu is outside the HUD proper, and other menu mods may restyle it. Low effort once bug 2 is fixed. |
| `RestyleTooltips` | Tooltips keep the game's look. | `TooltipStyler` already takes the font and panel settings as arguments, so this is one more argument. |
| `ShowHealthRegen` | Hides the "+12.4/s" beside the health numbers. | The game's HUD does not show regeneration; LookingGlass users see it twice. |
| `ObjectiveIcons` | Keeps the game's inline icons in objective text. | Off wraps "Charge the Teleporter" onto a second line, so the default should stay as it is. |

### Not recommended

- **A toggle per styler.** Seventeen switches would multiply the combinations the tests have to cover, and parts measure against each other (the prompt against the run info, the scoreboard against the item bar).
- **Splitting Barlow from Barlow Condensed.** The layouts are sized for the condensed digits.
- **Panel opacity or accent color.** Useful, but these are sliders and colors, not toggles, and `Palette` is all constants today. Treat as a separate feature.

### What each new setting needs

1. A field in `PluginConfig` with a one- or two-sentence description.
2. A `CheckBoxOption` in `RiskOfOptionsCompat`, before the Debug options.
3. A `SettingChanged` subscription in `SevenUIController`, so the change applies without a restart, and the matching unsubscribe.
4. A row in the README table and in `docs/DETAILS.md`, and a changelog line.
5. A line in `SevenUITests.Precondition`, and a scenario that turns it off and checks the game's look comes back.

Steps 3 and 5 are much cheaper after the `IStyler` refactor in section 6: each styler could carry its own "enabled" condition, and the controller would apply or restore it when that condition changes. Doing the refactor first is the recommended order.

## 8. Suggested order

The detailed task plan is in [AUDIT-PLAN.md](AUDIT-PLAN.md).

1. Install the current build and run the tests (section 1).
2. Fix bugs 1 to 3.
3. Add `IStyler`, `UiFactory` and `HudPaths` as one refactor on its own branch.
4. Add the recommended settings, one commit each.
5. Do the small clean-ups.
