using EntityStates.Bandit2.Weapon;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using TMPro;
using UnityEngine;

namespace SevenUI
{
    // Bandit's Desperado stacks (the BanditSkull buff, which the game shows nowhere on the skill) as a bold Barlow
    // Condensed number centered above the special slot, like the other counts on the row. Read from the HUD's target
    // body, so spectating shows the teammate's. Shown only while Desperado is the equipped special (Lights Out, which
    // doesn't use the stacks, keeps it hidden even while the buff is on) and there is at least one stack.
    internal sealed class DesperadoCount
    {
        private const string ObjectName = "SevenUIDesperadoCount";
        // Canvas pixels between the slot's top and the number's line box, and the box's height in sizes of the font.
        private const float Gap = 4f;
        private const float LineHeightShare = 1.2f;
        private const int Hidden = 0;

        private readonly HUD hud;
        private readonly RectTransform slot;
        private readonly TMP_Text count;
        private GameObject shownBody;
        private CharacterBody body;
        private SkillDef checkedDef;
        private bool checkedIsDesperado;
        private int shown = Hidden;

        private DesperadoCount(HUD hud, RectTransform slot, TMP_Text count)
        {
            this.hud = hud;
            this.slot = slot;
            this.count = count;
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
            text.alignment = TextAlignmentOptions.Bottom;
            UiFactory.KeepOnOneLine(text);
            text.gameObject.AddComponent<CondensedText>();
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, Gap / perUnit);
            rect.sizeDelta = new Vector2(0f, SkillSlotsStyler.StockFontSize * LineHeightShare / perUnit);
            text.gameObject.SetActive(false);
            recorder.Add(text.gameObject);
            return new DesperadoCount(hud, slot, text);
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
            }
            count.gameObject.SetActive(stacks > Hidden);
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
