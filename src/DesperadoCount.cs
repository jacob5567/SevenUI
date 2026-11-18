using EntityStates.Bandit2.Weapon;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Bandit's Desperado stacks (the BanditSkull buff, which the game shows nowhere on the skill) as a chip centered
    // above the special slot: the buff's icon in the text color, then the count as a bold Barlow Condensed number
    // (the number alone if the game has no icon for the buff). Read from the HUD's target body, so spectating shows
    // the teammate's. Shown only while Desperado is the equipped special (Lights Out, which doesn't use the stacks,
    // keeps it hidden even while the buff is on) and there is at least one stack.
    internal sealed class DesperadoCount
    {
        private const string ObjectName = "SevenUIDesperadoCount";
        public const string IconName = "SevenUIDesperadoIcon";
        // Canvas pixels between the slot's top and the number's line box, and the box's height in sizes of the font.
        private const float Gap = 4f;
        // Canvas pixels: the icon's side and the space between the icon and the number.
        public const float IconSize = 18f;
        public const float IconGap = 4f;
        private const float Half = 0.5f;
        private const float LineHeightShare = 1.2f;
        private const int Hidden = 0;

        private readonly HUD hud;
        private readonly RectTransform slot;
        private readonly TMP_Text count;
        private readonly RectTransform icon;
        private readonly SkillRow row;
        private float perUnit;
        private float laidOutFont;
        private int laidOutDigits;
        private GameObject shownBody;
        private CharacterBody body;
        private SkillDef checkedDef;
        private bool checkedIsDesperado;
        private int shown = Hidden;

        private DesperadoCount(HUD hud, RectTransform slot, TMP_Text count, RectTransform icon, SkillRow row, float perUnit)
        {
            this.hud = hud;
            this.slot = slot;
            this.count = count;
            this.icon = icon;
            this.row = row;
            this.perUnit = perUnit;
        }

        public bool Alive => count && slot;

        // Null when the HUD has no special slot to put it on.
        public static DesperadoCount Create(HUD hud, SkillIcon special, SkillRow row, StyleRecorder recorder)
        {
            if (!special || !special.stockText) return null;
            RectTransform slot = (RectTransform)special.transform;
            float perUnit = row.PixelsPer(slot);
            if (perUnit <= 0f) return null;
            TMP_Text text = UiFactory.NewText(ObjectName, slot, special.stockText, row.FontSize(slot, SkillSlotsStyler.StockFontSize),
                Palette.Text);
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.MidlineRight;
            UiFactory.KeepOnOneLine(text);
            text.gameObject.AddComponent<CondensedText>();
            // Both parts hang from the slot's top center, centered on the line box's middle; Layout sets their x.
            float middle = Gap / perUnit + SkillSlotsStyler.StockFontSize * LineHeightShare / perUnit * Half;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(Half, 1f);
            rect.pivot = new Vector2(1f, Half);
            rect.anchoredPosition = new Vector2(0f, middle);
            rect.sizeDelta = new Vector2(0f, SkillSlotsStyler.StockFontSize * LineHeightShare / perUnit);
            text.gameObject.SetActive(false);
            recorder.Add(text.gameObject);
            RectTransform iconRect = null;
            Sprite sprite = RoR2Content.Buffs.BanditSkull ? RoR2Content.Buffs.BanditSkull.iconSprite : null;
            if (sprite)
            {
                Image image = UiFactory.NewImage(IconName, slot, Palette.Text);
                image.sprite = sprite;
                image.preserveAspect = true;
                iconRect = image.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = new Vector2(Half, 1f);
                iconRect.pivot = new Vector2(0f, Half);
                iconRect.anchoredPosition = new Vector2(0f, middle);
                iconRect.sizeDelta = new Vector2(IconSize / perUnit, IconSize / perUnit);
                image.gameObject.SetActive(false);
                recorder.Add(image.gameObject);
            }
            return new DesperadoCount(hud, slot, text, iconRect, row, perUnit);
        }

        // A few reads a frame; the body is looked up again only when the HUD's target changes, and the text is written
        // (one short string) only when the count does.
        public void Update()
        {
            if (!Alive) return;
            int stacks = Stacks();
            if (stacks == shown) return;
            shown = stacks;
            if (stacks > Hidden)
            {
                count.text = stacks.ToString();
                count.gameObject.SetActive(true);
                if (icon)
                {
                    icon.gameObject.SetActive(true);
                }
                Layout(stacks);
            }
            else
            {
                count.gameObject.SetActive(false);
                if (icon)
                {
                    icon.gameObject.SetActive(false);
                }
            }
        }

        // Centers the icon and number as a pair; redone only when the number of digits or the font size changes.
        private void Layout(int stacks)
        {
            int digits = stacks.ToString().Length;
            if (digits == laidOutDigits && Mathf.Approximately(count.fontSize, laidOutFont)) return;
            if (!Mathf.Approximately(count.fontSize, laidOutFont))
            {
                float now = row.PixelsPer(slot);
                if (now > 0f)
                {
                    perUnit = now;
                }
            }
            laidOutDigits = digits;
            laidOutFont = count.fontSize;
            float numberWidth = count.preferredWidth;
            float iconWidth = icon ? (IconSize + IconGap) / perUnit : 0f;
            float left = -(iconWidth + numberWidth) * Half;
            count.rectTransform.sizeDelta = new Vector2(numberWidth, count.rectTransform.sizeDelta.y);
            Vector2 position = count.rectTransform.anchoredPosition;
            count.rectTransform.anchoredPosition = new Vector2(-left, position.y);
            if (icon)
            {
                icon.sizeDelta = new Vector2(IconSize / perUnit, IconSize / perUnit);
                icon.anchoredPosition = new Vector2(left, position.y);
            }
        }

        private int Stacks()
        {
            GameObject target = hud.targetBodyObject;
            if (target != shownBody)
            {
                shownBody = target;
                body = target ? target.GetComponent<CharacterBody>() : null;
            }
            if (!body || !body.skillLocator || !body.skillLocator.special) return Hidden;
            // The buff is read-only here; the game adds it on kills (GiveSkullOnKill).
            SkillDef special = body.skillLocator.special.skillDef;
            if (special != checkedDef)
            {
                checkedDef = special;
                // By what the skill runs, not its name or text, which change with the language.
                checkedIsDesperado = special && special.activationState.stateType == typeof(PrepSidearmSkullRevolver);
            }
            return checkedIsDesperado ? body.GetBuffCount(RoR2Content.Buffs.BanditSkull) : Hidden;
        }
    }
}
