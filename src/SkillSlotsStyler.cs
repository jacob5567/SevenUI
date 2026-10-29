using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the skill and equipment slots in the bottom right: flat dark slots, a top-down cooldown cover in place
    // of the radial overlay, cooldowns and stocks in Barlow Condensed, and the key labels (the slots' and the
    // inventory, sprint and key item buttons') as small grey capitals without their dark plates. The game keeps
    // running the slots.
    internal sealed class SkillSlotsStyler
    {
        private const string ClusterPath = "MainContainer/MainUIArea/SpringCanvas/BottomRightCluster";
        // The inventory and sprint buttons and the key item slot, left of the skills.
        private const string ExtraButtonsPath = "Scaler/ExtraButtonPanels";
        // The equipment slot's background, under its display root.
        private const string EquipmentBackgroundName = "BGPanel";
        private const string CoverName = "SevenUICooldownCover";

        // In HUD canvas units. The game draws cooldowns at the slot's full height; the canvas draws them smaller.
        private const float CooldownFontSize = 24f;
        private const float KeyFontSize = 13f;
        // 1px at 12px, in hundredths of an em.
        private const float KeyTracking = 8f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<SkillIcon> skills = new List<SkillIcon>();
        private readonly List<EquipmentIcon> equipment = new List<EquipmentIcon>();
        private readonly List<InputBindingDisplayController> keys = new List<InputBindingDisplayController>();

        public SkillSlotsStyler(Transform root)
        {
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
            Transform extraButtons = cluster.Find(ExtraButtonsPath);
            if (extraButtons)
            {
                StyleKeys(extraButtons);
            }
        }

        public void Restore()
        {
            recorder.Restore();
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
            if (slot.displayRoot)
            {
                Transform background = slot.displayRoot.transform.Find(EquipmentBackgroundName);
                Flatten(background ? background.GetComponent<Image>() : null);
            }
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
