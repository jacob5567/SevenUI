# Plan for working through the audit

This plan turns [AUDIT.md](AUDIT.md) into tasks that can each be handed to COPE (`/cope`) one at a time. Each task is one concern and one commit. The tasks are in the order to run them.

## Why this order

1. **Bug fixes first.** They are small and touch few files. Each one changes behavior a player can notice, and none of them depends on the refactor. Fixing them before the refactor also means the refactor moves correct code.
2. **Shared helpers before `IStyler`.** `UiFactory`, `HudPaths` and the shared constants change no behavior and shrink the files the `IStyler` task has to read. If `IStyler` went first, the helper task would have to edit everything twice.
3. **`IStyler` before any setting.** Each new setting then becomes an "enabled" condition on one styler, with no new wiring in the controller. `IStyler` also fixes bug 2.
4. **Performance after the refactor.** These edits land in files the refactor rewrites, such as `PickerPanel`, so doing them later avoids conflicts.
5. **Settings next**, cheapest first. Two tasks that edit the same styler run one after the other.
6. **Small clean-ups last.** The refactor will already have removed some of them, and the rest are cosmetic.

COPE's check is compile-only, and none of this can be tested outside the game. So each phase ends with a **checkpoint** where Jacob installs the build and runs the tests in game. Don't start the next phase until the checkpoint passes. A failure is much cheaper to trace within one phase than across three.

## Context to paste after every task

COPE sees nothing from earlier conversations. Append this block to every task below:

```
CONTEXT:
- Follow ~/source/RoR2Mods/CLAUDE.md (design principles, code style) and the
  repo's CLAUDE.md build notes. Finding numbers refer to docs/AUDIT.md.
- Check: `dotnet build -c Release -p:CopyToProfile=false`, 0 warnings. Never
  install into the r2modman profile and never run build.sh.
- Behavior can only be tested in game, which you cannot do. Where the task
  says so, add or extend a ModTestRunner scenario in tests/ so Jacob can.
  Say in the report what still needs an in-game check.
- Read game code from the decompiled assembly, saved as .txt outside the
  repo, never as .cs under it.
- One signed commit for the task, subject imperative and in sentence case.
  If GPG signing fails, stop and report; never use --no-gpg-sign. Do not
  push. Do not edit CHANGELOG.md (entries are added with the version bump).
```

## Phase A: bug fixes

Each task is small. The "Do" lines are the request to send to COPE.

**A1. A scoreboard row that failed to style is counted as styled** (finding 2.3)
Do: In `src/ScoreboardStyler.cs`, a row is added to `styled` before `StyleRow` runs, so a row that `StyleRow` returns early on is never retried. Make `StyleRow` report whether it styled the row, and add the row to `styled` only if it did. Rows that can never be styled must not be retried every frame, so check what makes `StyleRow` return early first.

**A2. Take the HUD from the controller, not from `root`** (finding 2.5)
Do: `HudFontSwapper.GameFontText()` and `ScoreboardStyler` (around line 332) call `root.GetComponent<HUD>()`, though the controller notes that root can differ from the HUD. Pass the `HUD` to `HudFontSwapper` the way the other stylers get it, and use the `hud` field `ScoreboardStyler` already has.

**A3. Apply `MonospaceDamageNumbers` with no HUD** (finding 2.4)
Do: `DamageNumberFont.Update` is driven only from `SevenUIController.LateUpdate`, so turning the setting off in the main menu has no effect until the next run. Subscribe to `MonospaceDamageNumbers.SettingChanged` in `SevenUIPlugin` (unsubscribing in `OnDestroy`) and apply it there as well. Keep the per-HUD call only if it is still needed, for example for a material that loads late.

**A4. Guard `KarmaRow.Set` against an inactive row** (finding 2.7)
Do: `KarmaRow.Set` lays out from `Root.rect.width` without checking that the row is active. `ArtifactRow` (line 103) returns early in that case. Trace whether `KarmaRow`'s caller always activates the row first. If it doesn't, add the same guard and make sure the layout runs again once the row is active. Needs an in-game check: a run with Karma on from the first frame.

**A5. Find the run info panel when a prompt panel needs it** (finding 2.6)
Do: `PromptsStyler` passes `RunInfoPanel()` to `PromptPanel` once, at construction, so a prompt made before the run info panel exists never lines up with it. Let `PromptPanel` look the panel up again while it is missing, and cache it once found.

**A6. Settle the two calls against the read-only principle** (finding 2.8)
Do: Decompile `EquipmentState.GetEquipmentDisabled` (or wherever it is defined) and confirm it is a pure read. If it is, add a comment saying so at `src/EquipmentDeck.cs:165`. If it isn't, re-implement the check from synced fields. At `src/ScoreboardStyler.cs:404-411`, add a comment explaining why calling `UpdateDisplay()` and `LayoutAllIcons()` is an allowed exception: they touch only local UI, with no run state and no network.

**A7. Drop the per-frame ally card unindent if it is redundant** (AUDIT section 1)
Do: `f2cbe07` sets `AllyCardManager.indentWidth` to 0, and `3dbd56e` added a per-frame `Unindent` and its dictionary to `src/AllyCardsStyler.cs` (around line 247). There is one manager per HUD, so the first is likely enough. Confirm from the decompiled `AllyCardManager.SetLayoutHorizontal` that `indentWidth` is the only source of the indent, and that the game does not reset it. If so, remove `Unindent` and its state.
Do this one only after Jacob agrees, because it removes a fix that is known to work.

**Checkpoint A:**
- Install with a plain `dotnet build -c Release`.
- Run `mod_test --mod SevenUI`.
- Start a run with Karma on.
- Toggle `MonospaceDamageNumbers` in the main menu.
- If A7 was done, look at the ally screenshots.

## Phase B: structure

Run these on one branch, `refactor/stylers`, in this order. They should change no behavior. The full test suite passing at the checkpoint is the proof.

**B1. `HudPaths` and shared constants** (findings 4 and 6)
Do: Move the `MainContainer/MainUIArea/SpringCanvas/...` paths repeated in eight files into one `HudPaths` static class. Move `OutlineEdgePixels`, the item slot size and gap, `DividerHeight` and `CanvasHeight = 1080f` to one definition each, in the class that owns the concept (for example `HudUnits`). Pure move: no value changes. If two copies differ, keep both and report it.

**B2. `UiFactory`** (finding 4)
Do: Replace the `NewObject`/`NewUIObject` copies in about 17 files, and the repeated `Stretch`, `NewText`/`AddText`, `NewImage`, `KeepOnOneLine` and "flatten image" steps, with one `UiFactory` static class. Pick one return type (`RectTransform`). Also make one helper each for the three copies of embedded-resource loading and the five copies of the camera `pixelRect` projection (in `HudMargins`, `TopBarsWidth`, `PromptsStyler`, `SkillRow` and `ScoreboardPlacement`). Where copies differ (layer, `CanvasRenderer`), keep what each caller needs as a parameter.

**B3. `IStyler` and one ordered styler list** (findings 6 and 2.2)
Do: `SevenUIController` names each of its stylers in `Awake`, `LateUpdate`, `ApplyPanels`, `RestorePanels` and `Describe`. Define `IStyler` with `Apply()`, `Restore()`, `Update()`, `Describe(StringBuilder)` and `bool Enabled { get; }`, and keep one list in apply order, with restore in the order `RestorePanels` uses today. Requirements:
- Keep the current apply and restore order exactly, and the comments that explain it (tilt first; margins after the panels and after the skill slots).
- Stylers whose `Update` takes arguments (`PauseScreenStyler`, `HudMargins`, `TooltipStyler`) read the settings themselves or get what they need through their constructors.
- `Enabled` replaces the `panelsApplied`/`marginsApplied` flags. The controller applies or restores a styler when its `Enabled` changes, and subscribes once to every setting any styler reads.
- Fix bug 2 here: `PauseScreenStyler` has no `Restore`. Give it one that undoes its styling of the pause menu while it is open, through its `StyleRecorder`. The menu is rebuilt each time it opens, so this matters only for the open one.
- Add a scenario that turns `FlatPanels` off and on with the pause menu open and checks that the menu's look follows.
- `TooltipStyler` and `DamageNumberFont` are not HUD stylers. Leave them outside the list if they don't fit, and say why in a comment.

**B4. Close the `StyleRecorder` gaps** (finding 4)
Do:
- `Record(LayoutElement)` saves only heights. Make it save widths and flexible sizes too.
- `Record(TMP_Text)` omits wrapping, overflow and auto-size. Make it save them.
- Make `OnRestore` run each callback once per `Restore`, so callers can drop their own run-once guards.
- Remove the hand-written patches in the callers that these changes make redundant.
- Prune entries for destroyed objects when recording (finding 3, last bullet).

**B5. Split `PickerPanel`** (finding 4)
Do: `src/PickerPanel.cs` is 864 lines covering the cards, grid, cancel button, legend and layout. Split it into one class per part. `PickerPanel` stays the owner that wires them together. No behavior change.

**Checkpoint B:**
- Install and run the full `mod_test --mod SevenUI`.
- By hand, toggle each setting from Risk Of Options during a run and from the pause menu, then check that the game's look comes back and goes away again.
- Then merge `refactor/stylers` into `main`.

## Phase C: performance

Small and independent of each other.

- **C1.** `PauseScreenStyler.RunText()` builds a string every frame while paused, and retries its `Find` every frame if styling failed. Rebuild the string only when its inputs change. Retry the `Find` only when the pause canvas's hierarchy count changes.
- **C2.** `PickerPanel` layout re-measures every option's description on each hover change, and Command has 100+ options. Cache the tallest description height per picker, and recompute it only when the options change.
- **C3.** `ProtectionStripes.LateUpdate` runs `GetComponentsInChildren` every frame on each styled health bar. Cache the components, and refresh the cache when the bar's child count changes.
- **C4.** `ScoreboardRow` compares the money text by reference, so a new but equal string triggers a re-measure. Compare by value.

**Checkpoint C:** install, then run the `pickers`, `scoreboard` and `panels` suites.

## Phase D: settings

Each task adds one setting and follows AUDIT section 7's "What each new setting needs", except the changelog line (see the context block). With `IStyler` in place, the work is mostly the styler's `Enabled` condition, the config entry, the Risk Of Options entry, the README and DETAILS rows, the `Precondition` line and a scenario that turns the setting off and checks the game's look comes back.

All default to true, sit in the `Panels` section and apply only with `FlatPanels` on. Use the "What off does" wording from AUDIT section 7 for each description.

| Task | Setting | Note |
|---|---|---|
| D1 | `EquipmentDeck` | Created in one place. |
| D2 | `RestyleItemPickers` | `PickerStyler` is its own styler. |
| D3 | `CompactAllyCards` | One branch in `AllyCardsStyler`'s fitting. |
| D4 | `GroupAllyCards` | After D3 (same styler). With it off, compact rows and "+N more" must still work for single cards. |
| D5 | `LevelHud` | `TiltFlattener` is its own styler. `SkillRow` and `HudMargins` measure through the camera, so they need an in-game check with the tilt on. |
| D6 | `ScoreboardDetailPanel` | Off brings back the game's tooltip, and the board's width must not keep the panel's room. |
| D7 | `ScoreboardTabs` | After D6 (same board). Off needs a shrink path in `ScoreboardItemsBoard`; check first whether the game's own layout already gives one. |

**Checkpoint D:** after each pair, install and run that part's suite plus the new scenarios, and look at the screenshots. D5 needs a careful look at the skill row and the margins with the tilt on.

**Before Phase D, Jacob decides:** which "Worth considering" settings to add. `RestylePauseMenu` is cheap after B3. `TimerHundredths` defaults to false.

## Phase E: small clean-ups

Run this as one COPE task, or two if the diff gets large. Skip anything the refactor already removed.

- In `SkillSlotsStyler`, rename the locals `extraButtons` and `row`, which shadow fields with the same names.
- Fix the broken comment wrapping in `HudMargins`, `RunInfoStyler`, `ScoreboardStyler`, `ScoreboardRow`, `SlotFitter` and `FlatIcons`.
- Drop the second `IsKarmaEnabled()` in `RunInfoStyler` and the duplicate `hovering`/`hovered` locals in `PickerButton.Update`.
- Remove the stray whitespace in `FlatSprite`.
- Rename `EquipmentIcons.loading` to `Loading`.
- Move `NoGrids` to the top of `ScoreboardDetailPane`.
- Make `EquipmentDeck.keys` `readonly`.
- Add a comment in `AllyMoreRow` that "+N more" is English-only, as `ScoreboardRow` notes.
- Leave the nested data classes (`ItemStatsText.Parts`, `CurrencyRow.Currency`) as they are.

**E2, separately:** `BuffRows` reads TMP internals (`m_IsTextBackingStringDirty`, `m_text`). Find out why. Replace them with public API if the same result is possible without them. If it isn't, document why they are needed.

**Checkpoint E:** install and run the full suite. Then release: bump the version in `thunderstore/manifest.json` and write the CHANGELOG entry for the player-visible changes (bugs 2–4, the new settings).

## What stays untestable

The in-game scenarios cannot cover these, so check them by hand:
- Toggling settings from the pause menu while it is open, if the B3 scenario can't drive the menu.
- `MonospaceDamageNumbers` in the main menu, with no run.
- The look with the tilt on (D5).
- Conflicts with other HUD mods (D1, D2), and playing as a client of an unmodded host.
