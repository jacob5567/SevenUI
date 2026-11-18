using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EntityStates.Bandit2.Weapon;
using ModTestRunner;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI.Tests
{
    // Bandit's Desperado stacks above the special slot: what is on the HUD, where it sits, and the setups the scenarios
    // share (a special in the loadout, stacks of the buff).
    internal static class DesperadoChecks
    {
        // The name the mod gives the count's object, read by the tests.
        private const string CountName = "SevenUIDesperadoCount";
        private const string IconName = "SevenUIDesperadoIcon";
        // The icon's side and its space from the number, in canvas pixels.
        private const float IconGap = 4f;
        // In canvas pixels. The count sits a few pixels above the slot; a pixel or so is perspective.
        private const float Tolerance = 1.5f;
        private const float MaxGap = 12f;
        public const int OneStack = 1;
        public const int FiveStacks = 5;
        public const int ManyStacks = 12;

        public static TMP_Text Count(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == CountName);

        public static Image Icon(Harness h) =>
            SevenUIChecks.HudRoot(h).GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == IconName);

        private static SkillIcon SpecialIcon(Harness h)
        {
            HUD hud = SevenUIChecks.HudRoot(h).GetComponent<HUD>();
            return hud && hud.skillIcons != null
                ? hud.skillIcons.FirstOrDefault(icon => icon && icon.targetSkillSlot == SkillSlot.Special)
                : null;
        }

        private static string Look(Harness h)
        {
            TMP_Text count = Count(h);
            CharacterBody body = Harness.LocalBody;
            GenericSkill special = body ? body.skillLocator.special : null;
            return $"count {(count ? $"'{count.text}' active={count.gameObject.activeInHierarchy}" : "missing")}; special " +
                   $"{(special && special.skillDef ? special.skillDef.skillName : "none")}, buff " +
                   $"{(body ? body.GetBuffCount(RoR2Content.Buffs.BanditSkull) : -1)}";
        }

        public static IEnumerator ExpectCountHidden(this Harness h, string when)
        {
            yield return h.Until(
                () =>
                {
                    TMP_Text count = Count(h);
                    Image icon = Icon(h);
                    return count && !count.gameObject.activeInHierarchy && (!icon || !icon.gameObject.activeInHierarchy);
                },
                () => $"no Desperado count or icon {when} ({Look(h)}) {h.Diagnosis()}");
        }

        // The buff's icon and the number showing the stacks, side by side and centered as a pair over the special slot, above
        // it without touching its cooldown or stock, and on screen.
        public static IEnumerator ExpectCountShows(this Harness h, int stacks)
        {
            string look = "";
            yield return h.Until(
                () =>
                {
                    TMP_Text count = Count(h);
                    SkillIcon special = SpecialIcon(h);
                    if (!count || !special || !count.gameObject.activeInHierarchy || count.text != stacks.ToString() ||
                        count.textInfo.characterCount != stacks.ToString().Length)
                    {
                        look = Look(h);
                        return false;
                    }
                    Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
                    Rect slot = CanvasSizeChecks.Box(h, (RectTransform)special.transform);
                    Rect drawn = DrawnBox(h, count);
                    Rect screen = CanvasLayoutChecks.ScreenOf(camera);
                    Rect screenBox = ScreenBox(camera, count);
                    Sprite expected = RoR2Content.Buffs.BanditSkull.iconSprite;
                    Image icon = Icon(h);
                    bool iconOk = !expected || (icon && icon.gameObject.activeInHierarchy && icon.sprite == expected &&
                                                SevenUIChecks.Near(icon.color, Palette.Text));
                    Rect pair = CanvasSizeChecks.Box(h, count.rectTransform);
                    float iconGap = 0f;
                    if (expected && icon)
                    {
                        Rect iconBox = CanvasSizeChecks.Box(h, icon.rectTransform);
                        iconGap = pair.xMin - iconBox.xMax;
                        pair = Rect.MinMaxRect(Mathf.Min(pair.xMin, iconBox.xMin), pair.yMin, pair.xMax, pair.yMax);
                        Rect iconScreen = ScreenBox(camera, icon.rectTransform);
                        screenBox = Rect.MinMaxRect(Mathf.Min(screenBox.xMin, iconScreen.xMin), Mathf.Min(screenBox.yMin, iconScreen.yMin),
                            Mathf.Max(screenBox.xMax, iconScreen.xMax), Mathf.Max(screenBox.yMax, iconScreen.yMax));
                    }
                    bool gapOk = !expected || Mathf.Abs(iconGap - IconGap) <= Tolerance;
                    float gap = drawn.yMin - slot.yMax;
                    float offCenter = pair.center.x - slot.center.x;
                    bool above = gap >= -Tolerance && gap <= MaxGap;
                    bool centered = Mathf.Abs(offCenter) <= Tolerance;
                    bool onScreen = screenBox.xMin >= screen.xMin && screenBox.xMax <= screen.xMax &&
                                    screenBox.yMin >= screen.yMin && screenBox.yMax <= screen.yMax;
                    string clash = Overlaps(h, drawn, special.cooldownText) ?? Overlaps(h, drawn, special.stockText);
                    look = $"{Look(h)}; digits {drawn.width:0.#}x{drawn.height:0.#}px, {gap:0.#}px above the slot, " +
                           $"{offCenter:0.#}px off its center, icon ok={iconOk}, icon gap {iconGap:0.#}px, on screen={onScreen}, " +
                           $"overlaps {clash ?? "nothing"}";
                    return above && centered && onScreen && clash == null && iconOk && gapOk;
                },
                () => $"the Desperado icon and count '{stacks}' centered above the special slot, on screen, clear of its cooldown and " +
                      $"stock ({look}) {h.Diagnosis()}");
        }

        // The slot's cooldown or stock text, when it shows and its digits' box meets the count's.
        private static string Overlaps(Harness h, Rect drawn, TMP_Text other)
        {
            if (!other || !other.gameObject.activeInHierarchy || other.textInfo.characterCount == 0) return null;
            return drawn.Overlaps(DrawnBox(h, other)) ? other.name : null;
        }

        // The digits' box in canvas pixels.
        private static Rect DrawnBox(Harness h, TMP_Text text)
        {
            Camera camera = CanvasLayoutChecks.HudCamera(SevenUIChecks.HudRoot(h));
            Rect box = ScreenBox(camera, text);
            float unit = CanvasLayoutChecks.ScreenOf(camera).height / CanvasHeight;
            return new Rect(box.x / unit, box.y / unit, box.width / unit, box.height / unit);
        }

        private const float CanvasHeight = 1080f;

        private static Rect ScreenBox(Camera camera, RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        // The glyphs' bounds on screen.
        private static Rect ScreenBox(Camera camera, TMP_Text text)
        {
            Bounds bounds = text.textBounds;
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.min));
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, text.transform.TransformPoint(bounds.max));
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x),
                Mathf.Max(min.y, max.y));
        }

        // Puts the body's special slot on the variant whose skill runs the state (Desperado: PrepSidearmSkullRevolver,
        // Lights Out: PrepSidearmResetRevolver), by a replacement the cleanup takes off again. Returns what takes it off
        // earlier, for a scenario that switches. Skips when the body has no such variant.
        public static Action EquipSpecial(Harness h, CharacterBody body, Type prepState)
        {
            GenericSkill special = body.skillLocator.special;
            SkillDef variant = special.skillFamily.variants.Select(entry => entry.skillDef)
                .FirstOrDefault(def => def && def.activationState.stateType == prepState);
            if (!variant) throw h.Skip($"the special's family has no variant running {prepState.Name}");
            object source = typeof(DesperadoChecks);
            special.SetSkillOverride(source, variant, GenericSkill.SkillOverridePriority.Replacement);
            bool removed = false;
            Action remove = () =>
            {
                if (removed || !special) return;
                removed = true;
                special.UnsetSkillOverride(source, variant, GenericSkill.SkillOverridePriority.Replacement);
            };
            h.OnCleanup(remove);
            return remove;
        }

        public static Type Desperado => typeof(PrepSidearmSkullRevolver);

        public static Type LightsOut => typeof(PrepSidearmResetRevolver);

        // Sets the stacks of Desperado's buff on the body, and takes them off at cleanup. The runner is the host.
        public static void SetStacks(Harness h, CharacterBody body, int stacks)
        {
            if (!body) return;
            body.SetBuffCount(RoR2Content.Buffs.BanditSkull.buffIndex, stacks);
            h.OnCleanup(() =>
            {
                if (body)
                {
                    body.SetBuffCount(RoR2Content.Buffs.BanditSkull.buffIndex, 0);
                }
            });
        }

        // Writes the stacks without queuing another cleanup.
        public static void SetStacksNow(CharacterBody body, int stacks)
        {
            if (body)
            {
                body.SetBuffCount(RoR2Content.Buffs.BanditSkull.buffIndex, stacks);
            }
        }
    }
}
