using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the skill, equipment and key item slots in the bottom right: flat dark slots, the equipment and key item
    // at the skills' size, a top-down cooldown cover in place of the radial overlay, cooldowns and stocks in Barlow
    // Condensed, and the key labels (the slots' and the inventory and sprint buttons') as small grey capitals without
    // their dark plates. The game keeps running the slots.
    internal sealed class SkillSlotsStyler
    {
        private const string ClusterPath = "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster";
        // The inventory and sprint buttons and the key item slot, left of the skills.
        private const string ExtraButtonsPath = "Scaler/ExtraButtonPanels";
        // The equipment and key item slots' background, under their display roots.
        private const string SlotBackgroundName = "BGPanel";
        private const string CoverName = "SevenUICooldownCover";

        // In HUD canvas units. The game draws cooldowns at the slot's full height; the canvas draws them smaller.
        private const float CooldownFontSize = 24f;
        private const float KeyFontSize = 13f;
        // 1px at 12px, in hundredths of an em.
        private const float KeyTracking = 8f;

        private readonly HUD hud;
        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<SkillIcon> skills = new List<SkillIcon>();
        private readonly List<EquipmentIcon> equipment = new List<EquipmentIcon>();
        private readonly List<KeyItemIcon> keyItems = new List<KeyItemIcon>();
        private readonly List<InputBindingDisplayController> keys = new List<InputBindingDisplayController>();
        private SlotFitter equipmentFit;
        private SlotFitter keyItemFit;

        public SkillSlotsStyler(HUD hud, Transform root)
        {
            this.hud = hud;
            this.root = root;
        }

        public void Apply()
        {
            Transform cluster = root.Find(ClusterPath);
            if (!cluster) return;
            cluster.GetComponentsInChildren(true, skills);
            foreach (SkillIcon skill in skills)
            {
                StyleSkill(skill);
            }
            skills.Clear();
            cluster.GetComponentsInChildren(true, equipment);
            foreach (EquipmentIcon slot in equipment)
            {
                StyleEquipment(slot);
            }
            equipment.Clear();
            // Before the key item, which is among the extra buttons and whose key is then sized to the skills'.
            Transform extraButtons = cluster.Find(ExtraButtonsPath);
            if (extraButtons)
            {
                StyleKeys(extraButtons);
            }
            cluster.GetComponentsInChildren(true, keyItems);
            foreach (KeyItemIcon slot in keyItems)
            {
                FlattenBackground(slot.displayRoot);
                FitKeyItem(slot);
                MatchSkillKeys(slot.displayRoot);
            }
            keyItems.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
            equipmentFit = null;
            keyItemFit = null;
        }

        // The equipment slot as drawn, the right end of the skill row, once fitted.
        public RectTransform EquipmentSlot => equipmentFit != null && equipmentFit.Alive ? equipmentFit.Display : null;

        // Every frame while applied, so the two slots follow the skills.
        public void Update()
        {
            equipmentFit?.Update();
            keyItemFit?.Update();
        }

        private void StyleSkill(SkillIcon skill)
        {
            Flatten(skill.GetComponent<Image>());
            // The game shows the ready outline by turning its object on and off, so turning off its image keeps it
            // hidden. The icon still goes grey while the skill isn't ready.
            if (skill.isReadyPanelObject)
            {
                recorder.Disable(skill.isReadyPanelObject.GetComponent<Image>());
            }
            if (skill.cooldownRemapPanel && skill.iconImage && !skill.transform.Find(CoverName))
            {
                AddCover(skill);
            }
            StyleNumbers(skill.cooldownText, skill.stockText);
            StyleKeys(skill.transform);
        }

        private void StyleEquipment(EquipmentIcon slot)
        {
            FlattenBackground(slot.displayRoot);
            // The equipment keeps its ready outline, thin and in the equipment tier's orange.
            Image ready = slot.isReadyPanelObject ? slot.isReadyPanelObject.GetComponent<Image>() : null;
            if (ready)
            {
                recorder.Record(ready);
                ready.sprite = FlatSprite.Outline;
                ready.type = Image.Type.Sliced;
                ready.color = Palette.Equipment;
            }
            StyleNumbers(slot.cooldownText, slot.stockText);
            StyleKeys(slot.transform);
            // The main slot; the alternate one (Executive Card's second equipment) is drawn smaller than the skills.
            if (!slot.displayAlternateEquipment && slot.displayRoot && slot.displayRoot.transform.Find(SlotBackgroundName) &&
                (equipmentFit == null || !equipmentFit.Alive))
            {
                equipmentFit = Fit(slot.displayRoot, false);
            }
            if (!slot.displayAlternateEquipment)
            {
                MatchSkillKeys(slot.displayRoot);
            }
        }

        // The equipment and key item slots are drawn at a larger scale than the skills, with their key plates further
        // below them, so their keys would come out bigger and lower than the skills' once the slots are the skills'
        // size. Their keys get the skills' size, and the main key plate the skills' distance below the slot. Both
        // plates sit at their slot's center, as the skills' do.
        private void MatchSkillKeys(GameObject displayRoot)
        {
            if (!displayRoot || hud.skillIcons == null || hud.skillIcons.Length == 0 || !hud.skillIcons[0]) return;
            InputBindingDisplayController skillKey = hud.skillIcons[0].GetComponentInChildren<InputBindingDisplayController>(true);
            RectTransform skillPlate = skillKey ? skillKey.transform.parent as RectTransform : null;
            if (!skillPlate || !skillPlate.parent) return;
            displayRoot.GetComponentsInChildren(true, keys);
            for (int i = 0; i < keys.Count; i++)
            {
                TMP_Text label = keys[i].GetComponent<TMP_Text>();
                if (label && label.transform.lossyScale.y > 0f)
                {
                    recorder.Record(label);
                    label.fontSize = KeyFontSize * skillKey.transform.lossyScale.y / label.transform.lossyScale.y;
                }
                // The first is the slot's own key; the rest are the equipment's swap keys, placed by the game.
                RectTransform plate = keys[i].transform.parent as RectTransform;
                if (i == 0 && plate && plate.parent == displayRoot.transform && !recorder.HasPosition(plate))
                {
                    float scale = skillPlate.parent.lossyScale.y / displayRoot.transform.lossyScale.y;
                    recorder.SetAnchoredPosition(plate, new Vector2(plate.anchoredPosition.x, skillPlate.anchoredPosition.y * scale));
                }
            }
            keys.Clear();
        }

        private void FitKeyItem(KeyItemIcon slot)
        {
            if (!slot.displayRoot || keyItemFit != null && keyItemFit.Alive) return;
            keyItemFit = Fit(slot.displayRoot, true, slot.extraDisplayHolder as RectTransform);
            if (keyItemFit == null) return;
            // The slot's layout room shrinks by as much as its display does, so the inventory and sprint buttons
            // keep their distance from it.
            LayoutElement element = slot.GetComponent<LayoutElement>();
            RectTransform slotRect = (RectTransform)slot.transform;
            RectTransform group = slotRect.parent as RectTransform;
            RectTransform skill = (RectTransform)hud.skillIcons[0].transform;
            if (!element || !group || element.preferredWidth <= 0f) return;
            float drawnWidth = slotRect.rect.width * slotRect.lossyScale.x / group.lossyScale.x;
            float skillWidth = skill.rect.width * skill.lossyScale.x / group.lossyScale.x;
            float preferred = element.preferredWidth;
            element.preferredWidth = Mathf.Max(preferred - (drawnWidth - skillWidth), 0f);
            recorder.OnRestore(() =>
            {
                if (element)
                {
                    element.preferredWidth = preferred;
                }
            });
        }

        // Beside the first skill (M1) or the last (R).
        private SlotFitter Fit(GameObject displayRoot, bool leftOfRow, RectTransform follower = null)
        {
            SkillIcon[] row = hud.skillIcons;
            if (row == null || row.Length < 2 || !row[0] || !row[1] || !row[row.Length - 1] || !row[row.Length - 2]) return null;
            Transform end = leftOfRow ? row[0].transform : row[row.Length - 1].transform;
            Transform inner = leftOfRow ? row[1].transform : row[row.Length - 2].transform;
            return new SlotFitter((RectTransform)displayRoot.transform, (RectTransform)end, (RectTransform)inner, leftOfRow, recorder,
                follower);
        }

        private void FlattenBackground(GameObject displayRoot)
        {
            if (!displayRoot) return;
            RectTransform background = displayRoot.transform.Find(SlotBackgroundName) as RectTransform;
            if (!background) return;
            Flatten(background.GetComponent<Image>());
            // The game's background reaches a little past the slot; flat, it fills the slot exactly, like a skill's.
            if (background.sizeDelta != Vector2.zero)
            {
                Vector2 size = background.sizeDelta;
                recorder.SetAnchoredPosition(background, Vector2.zero);
                background.sizeDelta = Vector2.zero;
                recorder.OnRestore(() =>
                {
                    if (background)
                    {
                        background.sizeDelta = size;
                    }
                });
            }
        }

        private void Flatten(Image image)
        {
            if (!image) return;
            recorder.Record(image);
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Palette.Trough;
        }

        // Hides the game's radial overlay as a group (the game turns it on and off and sets its color every frame) and
        // adds the cover just above the icon, under the cooldown number.
        private void AddCover(SkillIcon skill)
        {
            GameObject overlay = skill.cooldownRemapPanel.gameObject;
            if (!overlay.GetComponent<CanvasGroup>())
            {
                CanvasGroup group = overlay.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
                recorder.Add(group);
            }

            GameObject cover = new GameObject(CoverName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cover.layer = skill.gameObject.layer;
            RectTransform rect = (RectTransform)cover.transform;
            rect.SetParent(skill.transform, false);
            rect.SetSiblingIndex(skill.iconImage.transform.GetSiblingIndex() + 1);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = cover.GetComponent<Image>();
            image.color = Palette.CooldownCover;
            image.raycastTarget = false;
            image.enabled = false;
            CooldownCover follower = cover.AddComponent<CooldownCover>();
            follower.Overlay = skill.cooldownRemapPanel;
            follower.Cover = image;
            recorder.Add(cover);
        }

        private void StyleNumbers(TMP_Text cooldown, TMP_Text stock)
        {
            if (cooldown)
            {
                recorder.Record(cooldown);
                cooldown.fontSize = CooldownFontSize;
                cooldown.color = Palette.Text;
            }
            if (stock)
            {
                recorder.Record(stock);
                stock.fontStyle |= FontStyles.Bold;
                stock.color = Palette.Text;
            }
        }

        // The key labels (and the equipment's swap keys) sit on small dark plates; the plates go and the labels become
        // grey spaced capitals. A plate is the label's parent, unless the label sits right under the slot.
        private void StyleKeys(Transform slot)
        {
            slot.GetComponentsInChildren(true, keys);
            foreach (InputBindingDisplayController key in keys)
            {
                TMP_Text label = key.GetComponent<TMP_Text>();
                if (label)
                {
                    recorder.Record(label);
                    label.fontSize = KeyFontSize;
                    label.fontStyle = (label.fontStyle | FontStyles.UpperCase | FontStyles.Bold) & ~FontStyles.Italic;
                    label.characterSpacing = KeyTracking;
                    label.color = Palette.HeaderText;
                }
                Transform plate = key.transform.parent;
                if (plate && plate != slot)
                {
                    recorder.Disable(plate.GetComponent<Image>());
                }
            }
            keys.Clear();
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled skill slot values: {recorder.Count}");
            Transform cluster = root.Find(ClusterPath);
            if (!cluster) return;
            foreach (CooldownCover cover in cluster.GetComponentsInChildren<CooldownCover>(true))
            {
                output.AppendLine($"  {cover.transform.parent.name}: cooldown cover {cover.ShownRemaining:P0}");
            }
        }
    }
}
