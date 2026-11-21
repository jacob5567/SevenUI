# Plan for the open Super Productivity tasks

This plan orders the open tasks in the SevenUI project in Super Productivity: 23 as of 2026-11-15 (Phases A to G), and the ten added on 2026-11-20 with four older unplanned bugs (Phases H to J). Each task's notes there hold the detail: repro, design and scope. This file only adds the order, the reasons for it and what each task depends on. Task titles are shortened; estimates are the ones in Super Productivity and total about 170 hours for Phases A to G and 61 for H to J (H10 is B4, counted in both).

## Why this order

1. **Bugs before features.** Seven of the eight bugs are in the ally cards and the scoreboard, and the larger features build on those same two parts.
2. **Ally card bugs first.** Two of them show wrong information (a dead X over living drones, two drone tiers on one card). The scoreboard bugs are cosmetic or about speed.
3. **Tasks that edit the same file run one after the other**, with the one that changes shared data first. In the ally cards, the grouping key goes first because the other three fixes read it.
4. **The scoreboard's speed is fixed before anything is added to the board.** The inspect panel and the allies list both add work on hover, so the profile should be taken before them and kept as the baseline.
5. **Features go cheapest first**, and those that reuse an existing styler go before those that need a new design.
6. **The three large design jobs go last.** Each needs mock-ups and decisions from Jacob before any code. The mock-ups can be started early, alongside the earlier phases, because they touch no code.

## How each phase is built, tested and merged

ModTestRunner's `run-tests.sh` runs the in-game tests unattended (headless gamescope, 2560x1440 by default), so Claude runs them itself. Jacob is needed only for the manual checks at the end of a phase, which the tests can't do, and to approve the merge.

1. **Develop.** Each task goes to COPE, which works in its own worktree under `.claude/worktrees/` on its own branch, builds the mod and its tests with `-p:CopyToProfile=false`, and commits. A task that depends on another branches from it. Ticket tag: `in-progress`.
2. **Review.** The main session reads the diff, rebuilds the mod and the tests, and fixes small problems. Ticket tag: `verification`.
3. **Integrate.** The main session makes a phase branch (`phase/<letter>`) from `main` in the main checkout and merges the phase's task branches into it. Only the main session installs into the r2modman profile, from the main checkout: `dotnet build -c Release` there, then `ModTestRunner/build-tests.sh` on it.
4. **Test.** The main session runs `run-tests.sh --mod SevenUI --suite <the suites the phase touches>` first, then the full `run-tests.sh --mod SevenUI`, and reads `results.json`, the BepInEx log (no exceptions or warnings from SevenUI) and the screenshots of the new scenarios. A failure goes back to COPE on the task's branch, with the failure message and log lines; the fix is merged into the phase branch again and the tests rerun. This repeats until the full run passes, with every skip explained.
5. **Manual check.** Jacob gets one short list per phase: the checks the tests can't make (see each phase's "Manual check" line), the screenshots worth a look, and the result of the full run. Everything else is already verified.
6. **Merge.** With Jacob's approval, `phase/<letter>` merges into `main`, the task worktrees and branches are removed, and the tickets are marked done.

Phases A, B and C1 can be in progress at once: each has its own phase branch, tested one after another (the game runs once at a time).

## Phase A: ally card bugs (11 h)

All four edit `AllyCardsStyler` and its cards, so each branches from the one before it where they overlap.

| # | Task | Est. | Depends on |
|---|---|---|---|
| A1 | Different drone tiers share one card | 3 h | none |
| A2 | Dead X over a stack that still has living drones | 5 h | A1 |
| A3 | Players must stay full size and at the top | 2 h | A1 |
| A4 | Barrier covers the whole bar on compact rows | 1 h | none |

- A1 goes first because it changes the grouping key (name and leader, plus tier). A2 asks which card owns a kind after a death or a scrap, and the answer depends on that key. A1's notes also suspect a stale per-body name cache, which may be A2's cause too, so check it once in A1.
- A3 changes the fitting (which cards go compact or fold into "+N more"). Do it after A2 so the two don't both rewrite the card ordering.
- A4 is independent and small. It is here only because it is the same styler.

**Tests:** `panels` and `ally_portraits`, then the full run.
A3's scenario uses stand-in players, as the `scoreboard_leave` suite does (AI Commandos made players on the host). The game shows a player's ally card only if the player has a network user, so the test links the stand-ins to one.
**Manual check:** scrapping a different drone leaves a shared card alone (the tests can't scrap).

## Phase B: scoreboard bugs (10 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| B1 | Equipment outline is unevenly thick | 2 h | none |
| B2 | Unequal side padding in the tabbed board | 2 h | none |
| B3 | Hide the holders list with one player | 1 h | none |
| B4 | Frame rate drops when hovering the item list | 5 h | B1, B3 |

- B1 replaces the sliced outline sprite with `PixelOutline`. The same sprite is still used at `ScoreboardStyler.cs:509` and `:548`, `ScoreboardItemGrid.cs:268` (the hovered item highlight) and `SkillSlotsStyler.cs:180` and `:266`. Check all five in this task.
- B4 goes last because B1 changes how the hover highlight is drawn and B3 changes what the detail panel builds. Profiling before them would measure code that is about to change.

**Tests:** `scoreboard`, `scoreboard_rows`, `scoreboard_tabs` and `scoreboard_leave`, then the full run. The tests run at 1440p, where B1's uneven outlines showed, and B1's check measures each side. B4 adds a scenario that hovers across the crowded board and logs the time per hover, before and after.
B3's scenario adds and removes a stand-in player.
**Manual check:** the hover feels smooth on the crowded board (B4).

## Phase C: last bug and the mod audit (5 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| C1 | Operator's and Drifter's displays are too big | 2 h | none |
| C2 | Check that my other mods fit with SevenUI (audit only) | 3 h | A, B |

- C2 changes no code and produces follow-up tasks. It goes after A and B so the audit looks at the fixed layout, and before the features so its follow-ups can be slotted into Phase D.
- C2 is mostly in-game time for Jacob. Claude can prepare the checklist, read each mod's code for what it attaches to, and take screenshots with each mod's own test scenarios while SevenUI is installed.
- Operator is `DroneTech` in code (`DRONETECH_BODY_NAME`).

**Tests for C1:** `panels` and `screenshots`, then the full run.
**Manual check for C1:** the Operator and Drifter screenshots look right beside the skill row.

**Release point:** a bug-fix release (1.4.1) with A, B and C1.

## Phase D: small features on existing parts (17 h)

These are independent of each other. They are listed cheapest first.

| # | Task | Est. | Depends on |
|---|---|---|---|
| D1 | Empty state for the Lemurian egg picker | 2 h | none |
| D2 | Desperado stacks above the special skill | 3 h | none |
| D3 | Restyle the MusicDisplay mod's panel | 3 h | C2 |
| D4 | Shield and other effects on the boss bar | 5 h | none |
| D5 | Restyle the "moon detonation" countdown and other timers | 5 h | none |
| D6 | Restyle the "confirm quit" dialog and similar ones | 5 h | none |

- D3 follows C2 because both are about another mod's panel inside SevenUI's look. If the audit shows a shared need, such as one way for another mod's text to pick up Barlow, build it once and use it in D3.
- D4, D5 and D6 each start with a list made from the assembly (what the vanilla boss bar shows, every HUD countdown, every dialog reachable in a run). Agree the list before the styling starts.
- Follow-up tasks from C2 that belong in SevenUI go in this phase.

**Tests:** each task's new scenario, then the full run. D3's scenario skips without MusicDisplay, so install it in the profile for the phase's run.
**Manual check:** the new screenshots, and D2 and D4 as a client of an unmodded host. D5's escape sequence by hand only if the host can't start it.

## Phase E: new panels (19 h)

Each needs a mock-up agreed before the code.

| # | Task | Est. | Depends on |
|---|---|---|---|
| E1 | Redesign the scoreboard's inspect panel | 8 h | B |
| E2 | LookingGlass stats in the inspect panel | 3 h | E1 |
| E3 | Restyle the drone scrapper's panel | 8 h | D1 |

- E1 reuses `ScoreboardDetailPane`'s layout, so it goes after B3 and B4 have settled that file.
- E3 goes after D1 because both extend the picker styling, and D1 is the smaller change to it.

**Release point:** a feature release (1.5.0) with D and E.

## Phase F: allies on the scoreboard, and pinning (21 h)

One task in Super Productivity; split it in two.

| # | Task | Depends on |
|---|---|---|
| F1 | Show each player's allies on the scoreboard | A1, B4, E1 |
| F2 | Pin an ally kind so it keeps a full card near the top | A3, F1 |

- F1 needs A1 for the grouping key (it should reuse the ally cards' kinds, not build a second grouping) and B4 so the board is fast before it grows.
- F2 extends A3's rule (players exempt from compacting, sorted first) to pinned kinds, and it needs F1 if pinning is done by clicking on the board.

**Jacob decides before F2 starts:** how to pin (click on the board, a key, or something else), whether pins last across stages and runs, and whether a stack is identified by kind, leader and tier. These questions are in F2's ticket.

**Release point:** 1.8.0 with F1. F2 is out of this release; Jacob is working on it separately.

## Tasks added after 1.8.0 (Phases H to J)

Ten tasks were triaged on 2026-11-20, after 1.8.0. Four older bugs had no phase yet, and B4 (the hover frame rate) is still open, so those go in here too. Phases H to J come before Phase G: G2's picker backgrounds should be designed for the restyled item scrapper (J2), and G3's character select may reuse the dialog design (J1).

Why this order:
1. **Bugs first, in one bug-fix release.** They are small, independent of each other except where noted, and none needs a design decision.
2. **Shared data before what reads it.** The tier colors (H1) are used by the inspect panel, the pickers and the scrapper, so they are settled before I2, I3 and J2 draw anything with them. The count audit (H4) decides which stacks each count includes before H5 changes how counts look and I2 adds a corruption mark next to the picker count.
3. **One file, one task at a time.** H4, H5 and I2 all touch the picker count (`PickerButton`); I3 and I2 both change `ScoreboardInspect`, size first, then the new line.
4. **Mock-up tasks last, mock-ups early.** I2, I3, J1 and J2 each need an agreed mock-up before code. The mock-ups touch no code, so they can be made while Phase H is in progress.

### Phase H: bugs (29 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| H1 | Item rarity colors differ in tooltips, inspect panel and scoreboard | 3 h | none |
| H2 | Equipment description wrong with a Functional Coupler | 3 h | none |
| H3 | Moon detonation timer hard to read over the white sky | 3 h | none |
| H4 | LookingGlass and SevenUI counts both showing; temporary and Ethereal stacks | 5 h | none |
| H5 | Item count clashes with the cooldown ring and the Ethereal mark | 3 h | H4 |
| H6 | Less padding on the status effect display in the lower left | 1 h | none |
| H7 | Equal padding between the bars on a shared ally card | 3 h | none |
| H8 | The checked objective checkbox matches the unchecked one's size | 3 h | none |
| H9 | Operator's and Drifter's displays don't overlap the Functional Coupler's deck | 2 h | none |
| H10 | Frame rate drops when hovering the item list (B4, still open) | 5 h | H1, H2 |

- H1 routes every tier color through one helper (`GameColors.ToCanvas` or `TierGroups`). It goes first because I2, I3 and J2 draw tier colors.
- H4 is an audit first: list each count and the inventory method it uses, then fix double counts. H5 then fixes the look with the counts settled.
- H2 and H10 both touch the scoreboard's hover path (`ScoreboardItemsBoard.Point`, `ScoreboardDetailPane`), so H10 is profiled last, after H1 and H2 change it. Since B4 was planned, the inspect panel and the allies panel have added work on hover; profile with both open.
- H9 and H2 are unrelated despite both being about the Functional Coupler: H9 is the HUD's equipment deck (`EquipmentDeck`) beside the survivor displays, H2 is the scoreboard.
- H6 to H8 are small and independent: `BuffRows`, `AllyCardsStyler`'s segments, `ObjectiveCheckbox`.

**Tests:** `panels`, `objectives`, `ally_portraits`, `countdowns`, `pickers`, `scoreboard` and `scoreboard_inspect`, then the full run. New scenarios: one item of each tier compared across tooltip, inspect and pane strips (H1); a coupler swap with the pane open (H2); the escape countdown over a white backdrop (H3); a permanent, a temporary and an Ethereal stack of one item, checking each count (H4, H5); the hover timing scenario from B4 (H10).
**Manual check:** the moon's real escape sequence looking at the sky (H3); a Winnowing trial's Ethereal items in the item bar (H5); how smooth the hover feels on a crowded board (H10).

**Release point:** a bug-fix release, 1.8.1.

### Phase I: small features (11 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| I1 | Drone tiers as short colored tags ("T2", "T3") | 3 h | F2, or agreed with it |
| I2 | Warn when an item you might pick would be corrupted | 5 h | H1, H4, I3 |
| I3 | Make the inspect panel slightly larger | 3 h | H1 |

- I1 changes how `AllyNames` builds a name (the base name plus a tier, drawn as a tag in a color per tier). F2 (pinning), which Jacob is doing separately, identifies a stack by kind, leader and tier in the same code. Do I1 after F2 merges, or agree first that both use the tier number, never the display text.
- I3 goes before I2 because I2 adds a line to the inspect panel, and the panel's size should be settled first. Decide in I3 whether the allies panel keeps the inspect panel's width (it reads `ScoreboardInspect.WidthShare`).
- I2 marks the option in pickers and adds a line in the inspect panel, as Jacob agreed. It reads the synced inventory and `ContagiousItemManager`, as `PickupDescriptions.Contagious` does.
- Mock-ups for I2 and I3 can be made during Phase H.

**Tests:** `pickers`, `scoreboard_inspect`, `ally_portraits` and `scoreboard_allies`, then the full run. I1: upgraded drones show the tag in its color on the cards and the scoreboard tiles. I2: a held Lysate Cell marks its original item in a Command picker and on the ground.
**Manual check:** the new mock-up-led looks (I2, I3), and the tier colors on a crowded ally list.

**Release point:** a feature release, 1.9.0.

### Phase J: redesigns (21 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| J1 | Further redesign of "confirm quit" and similar dialogs | 8 h | none |
| J2 | Restyle all scrappers, with one design for the item and drone scrappers | 13 h | H1 |

- Both need mock-ups agreed with Jacob first. Make them during Phases H and I.
- J1 changes `DialogBoxStyler`, which also runs in the menus outside a run. G3 (character select) is the other screen outside a run, so J1's design should be one G3 can reuse.
- J2 adds a `PickerSource` for the item scrapper's scrolling list, which `PickupSource.For` skips today, and moves the drone scrapper (`DroneSource`) to the shared design. Split it into a design task and an implementation task once the design is agreed if it grows.

**Tests:** `panels` (the dialog checks) and `pickers` (the item scrapper with few and many items, as the drone scrapper is tested with 3 and 9), then the full run.
**Manual check:** the dialogs in the main menu and in a run; scrapping with a gamepad and with the mouse.

**Release point:** a feature release, 1.10.0. Then Phase G, whose G2 now also depends on J2.

## Phase G: large design jobs (81 h)

| # | Task | Est. | Depends on |
|---|---|---|---|
| G1 | New icons for Drifter's display and the Radar Scanner | 13 h | C1 |
| G2 | A background for each item picker | 34 h | D1, E3, J2 |
| G3 | Restyle the character select screen | 34 h | D6 |

- G1 follows C1 so the icons are drawn at the display's final size.
- G2 follows D1, E3 and J2 so the backgrounds are designed for every picker state, including the empty one, and for both scrappers in their shared design. Split it into one subtask per picker once the designs are agreed, as its notes say.
- G3 is last. It is the only task outside a run, so it needs a controller that isn't tied to a HUD. D6 gives the first dialog styling that may carry over.
- Each is its own release.

**Start early:** the lists and mock-ups for G1, G2 and G3 need no code changes, so they can be made at any time while Phases A to J are in progress.

## What the tests can't cover

These stay with Jacob, at the phase's manual check:
- Anything as a client of an unmodded host (D4 reads synced health; D2 reads synced buffs). The runner hosts the run, and extra players are stand-ins on the host, so nothing is tested as a client.
- How smooth the hover feels (B4): a scenario can time it but not judge it.
- Whether the look is right, for every mock-up-led task (E, G, I2, I3, J) and every new screenshot, and whether text is readable over a bright background (H3). The tests check sizes and places, not taste.
