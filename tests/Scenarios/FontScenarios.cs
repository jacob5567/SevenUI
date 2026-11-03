using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModTestRunner;
using RoR2;
using TMPro;
using UnityEngine;

namespace SevenUI.Tests
{
    // The BarlowFont setting: HUD text is drawn in Barlow (the run timer in Barlow Condensed), including text the HUD
    // gains later, and goes back to the game's font when the setting is turned off.
    internal static class FontScenarios
    {
        // Any hit that procs: the lenses do the killing.
        private const float FakeNumbersHit = 10f;
        // How far a line's middle may sit from its capitals' middle, as a share of the font's size.
        private const float CapitalsTolerance = 0.005f;

        public static IEnumerable<Scenario> All()
        {
            yield return new Scenario(1, "hud_text_uses_barlow", Stages.TitanicPlains, HudTextUsesBarlow);
            yield return new Scenario(1, "barlow_lines_centered_on_capitals", Stages.TitanicPlains, BarlowLinesCenteredOnCapitals);
            yield return new Scenario(1, "new_item_icons_use_barlow", Stages.TitanicPlains, NewItemIconsUseBarlow);
            yield return new Scenario(1, "timer_uses_barlow_condensed", Stages.TitanicPlains, TimerUsesBarlowCondensed);
            yield return new Scenario(1, "level_uses_barlow_condensed", Stages.TitanicPlains, LevelUsesBarlowCondensed);
            yield return new Scenario(1, "sleeping_text_keeps_barlow", Stages.TitanicPlains, SleepingTextKeepsBarlow);
            yield return new Scenario(1, "damage_numbers_use_monospace", Stages.TitanicPlains, DamageNumbersUseMonospace);
            yield return new Scenario(1, "damage_numbers_narrowed_on_next_stage", Stages.TitanicPlains, DamageNumbersNarrowedOnNextStage);
            yield return new Scenario(1, "lens_fake_numbers_narrowed", Stages.TitanicPlains, LensFakeNumbersNarrowed);
            yield return new Scenario(1, "turning_off_restores_game_font", Stages.TitanicPlains, TurningOffRestoresGameFont);
            yield return new Scenario(1, "lookingglass_stats_use_barlow", Stages.TitanicPlains, LookingGlassStatsUseBarlow);
        }

        private static IEnumerator HudTextUsesBarlow(Harness h)
        {
            yield return h.ExpectHudBarlow();
        }

        // Every Barlow font the HUD's text uses, and its bold and italic, has its line's middle on its capitals' middle,
        // so text centered by its line, or sized to it, centers its letters (FontBaker.CenterOnCapitals).
        private static IEnumerator BarlowLinesCenteredOnCapitals(Harness h)
        {
            yield return h.ExpectHudBarlow();
            HashSet<TMP_FontAsset> fonts = new HashSet<TMP_FontAsset>();
            foreach (TMP_Text text in SevenUIChecks.HudRoot(h).GetComponentsInChildren<TMP_Text>(true))
            {
                if (!text.font || !text.font.name.StartsWith("Barlow")) continue;
                fonts.Add(text.font);
                foreach (TMP_FontWeightPair pair in text.font.fontWeightTable ?? new TMP_FontWeightPair[0])
                {
                    if (pair.regularTypeface) fonts.Add(pair.regularTypeface);
                    if (pair.italicTypeface) fonts.Add(pair.italicTypeface);
                }
            }
            if (fonts.Count == 0) throw h.Fail("no HUD text uses Barlow");
            List<string> off = new List<string>();
            foreach (TMP_FontAsset font in fonts)
            {
                UnityEngine.TextCore.FaceInfo face = font.faceInfo;
                float lineMiddle = (face.ascentLine + face.descentLine) / 2f;
                float capitalsMiddle = face.capLine / 2f;
                if (Mathf.Abs(lineMiddle - capitalsMiddle) > CapitalsTolerance * face.pointSize)
                {
                    off.Add($"{font.name}: line middle {lineMiddle:0.##}, capitals' middle {capitalsMiddle:0.##} of {face.pointSize}");
                }
            }
            if (off.Count > 0) throw h.Fail($"Barlow lines not centered on their capitals: {string.Join("; ", off)}");
            h.Log($"{fonts.Count} Barlow fonts centered on their capitals: {string.Join(", ", fonts.Select(font => font.name))}");
        }

        // Stacked items add item icons with stack counts, which are created after the HUD was first restyled.
        private static IEnumerator NewItemIconsUseBarlow(Harness h)
        {
            yield return h.ExpectHudBarlow();
            h.Cmd($"{Commands.GiveItem} {GameNames.Syringe} 3");
            h.Cmd($"{Commands.GiveItem} {GameNames.Hoof} 2");
            yield return h.Wait(0.5f);
            yield return h.ExpectHudBarlow();
        }

        private static IEnumerator TimerUsesBarlowCondensed(Harness h)
        {
            yield return h.ExpectTimerCondensed();
        }

        private static IEnumerator LevelUsesBarlowCondensed(Harness h)
        {
            yield return h.ExpectLevelCondensed();
        }

        private static IEnumerator SleepingTextKeepsBarlow(Harness h)
        {
            yield return h.ExpectSleepingTextBarlow();
        }

        private static IEnumerator DamageNumbersUseMonospace(Harness h)
        {
            yield return h.ExpectDamageNumbersMonospace(true);
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, false);
            yield return h.ExpectDamageNumbersMonospace(false);
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, true);
            yield return h.ExpectDamageNumbersMonospace(true);
        }

        // Each stage makes a new damage number manager, whose particle is the game's width again, while the shared
        // material keeps the mod's sheet: the stretched digits would draw wide in square cells.
        private static IEnumerator DamageNumbersNarrowedOnNextStage(Harness h)
        {
            yield return h.ExpectDamageNumbersMonospace(true);
            DamageNumberManager before = DamageNumberManager.instance;
            yield return h.GoTo(Stages.DistantRoost);
            yield return h.Until(() => DamageNumberManager.instance && DamageNumberManager.instance != before,
                "a new damage number manager on the next stage");
            yield return h.ExpectDamageNumbersMonospace(true);
        }

        // Lost Seer's Lenses' execute effect bursts fake crit numbers with the damage numbers' material, from a
        // particle system of its own: narrowed with the real numbers, and back to the game's width with them.
        private static IEnumerator LensFakeNumbersNarrowed(Harness h)
        {
            LensInstakills.GiveLenses(h);
            yield return InstakillShowsFakeNumbers(h, true);
            h.SetConfig(SevenUIPlugin.Settings.MonospaceDamageNumbers, false);
            yield return h.ExpectDamageNumbersMonospace(false);
            yield return InstakillShowsFakeNumbers(h, false);
        }

        private static IEnumerator InstakillShowsFakeNumbers(Harness h, bool monospace)
        {
            List<CharacterBody> lemurians = new List<CharacterBody>();
            yield return LensInstakills.SpawnLemurians(h, 1, lemurians);
            LensInstakills.Hit(lemurians[0], FakeNumbersHit, false);
            yield return h.ExpectFakeNumbersFit(monospace);
        }

        private static IEnumerator TurningOffRestoresGameFont(Harness h)
        {
            yield return h.ExpectHudBarlow();
            h.SetConfig(SevenUIPlugin.Settings.BarlowFont, false);
            yield return h.ExpectHudGameFont();
            h.SetConfig(SevenUIPlugin.Settings.BarlowFont, true);
            yield return h.ExpectHudBarlow();
        }

        // LookingGlass copies the objective panel for its stats, so the copy must end up in Barlow too.
        private static IEnumerator LookingGlassStatsUseBarlow(Harness h)
        {
            Transform panel = null;
            yield return h.WaitForLookingGlassPanel(found => panel = found);
            yield return h.ExpectAllBarlow(panel, "LookingGlass's stats panel");
        }
    }
}
