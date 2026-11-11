using System.Collections.Generic;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The equipment a Functional Coupler's swap (Interact + Equipment) or MUL-T's Retool switches to, as a deck of bare
    // icons above the equipment slot: the next one right-aligned over the slot, with a small chevron pointing down into
    // it and, for a Coupler's swap, "E + Q" (the player's own bindings) to its left, and the ones after it rising behind it, each fainter, in
    // the order the swap reaches them. The game draws only the next one, in a slot of its own right of the equipment
    // slot, which the tight margins put off the screen. That slot keeps running, invisible, and the next icon copies its
    // icon, its grey while recharging and its cooldown; the later icons are read from the inventory. When the active
    // equipment changes, the next icon grows down into the slot while the deck moves down a place and the equipment
    // that was in use shrinks into its place at the back.
    internal sealed class EquipmentDeck
    {
        public const string Name = "SevenUIEquipmentDeck";
        public const string IconName = "Icon";
        public const string KeysName = "Keys";
        public const string ArrowName = "Arrow";
        public const string SwapName = "SevenUIEquipmentSwap";
        // The icons shown: the next and two behind it; a fifth equipment and on stay out of sight.
        public const int MaxShown = 3;
        public const float SwapSeconds = 0.25f;

        // Shares of the slot's side (the canvas's 90px slot at 1440p): the icons are 54, 18 above the slot, each later
        // one 32 higher; the keys end 12 left of the next icon; the chevron is drawn in a 36 square across the gap.
        private const float IconShare = 0.6f;
        private const float GapShare = 0.2f;
        // The game's equipment icons leave about this share of their picture empty at the bottom (2 to 12%), so the
        // chevron sits halfway between the slot and where a typical icon's picture starts, not its box.
        private const float IconPaddingShare = 0.05f;
        private const float StepShare = 0.36f;
        private const float KeysGapShare = 0.13f;
        private const float KeysWidthShare = 2f;
        private const float ArrowShare = 0.4f;
        // The later icons' opacity, as on the design page.
        private static readonly float[] Fades = { 1f, 0.55f, 0.32f };
        // In the design canvas's pixels.
        private const float KeySpacing = 4f;
        private const float CooldownFontSize = 18f;

        private readonly EquipmentIcon main;
        private readonly EquipmentIcon alternate;
        private readonly GameObject deck;
        private GameObject keys;
        private readonly RawImage[] icons = new RawImage[MaxShown];
        private readonly TMP_Text cooldown;
        private readonly GameObject swap;
        private readonly RawImage growing;
        private readonly List<EquipmentIndex> queue = new List<EquipmentIndex>();
        private Inventory inventory;
        private EquipmentLocation location;
        private Texture lastNext;
        private float swapStart = -1f;
        // While the swap plays, how many equipment the deck holds, so the one that was in use shrinks into the back.
        private int swapCount;

        public EquipmentDeck(EquipmentIcon main, EquipmentIcon alternate, RectTransform slot, SkillRow row, StyleRecorder recorder)
        {
            this.main = main;
            this.alternate = alternate;
            // Kept when the look is turned off, only shown again: turned straight back on, a new group couldn't be
            // added while the old one waited to be destroyed at the frame's end.
            CanvasGroup hidden = alternate.GetComponent<CanvasGroup>();
            if (!hidden)
            {
                hidden = alternate.gameObject.AddComponent<CanvasGroup>();
            }
            hidden.alpha = 0f;
            hidden.interactable = false;
            hidden.blocksRaycasts = false;
            recorder.OnRestore(() =>
            {
                if (!hidden) return;
                hidden.alpha = 1f;
                hidden.interactable = true;
                hidden.blocksRaycasts = true;
            });

            RectTransform root = NewObject(Name, slot);
            SetAnchors(root, Vector2.zero, Vector2.one);
            // Furthest first, so the next icon is drawn over the ones behind it.
            for (int i = MaxShown - 1; i >= 0; i--)
            {
                RawImage icon = NewObject(IconName + i, root).gameObject.AddComponent<RawImage>();
                icon.raycastTarget = false;
                icon.color = Color.clear;
                Place(icon.rectTransform, i);
                icons[i] = icon;
            }
            cooldown = NewLabel("Cooldown", icons[0].rectTransform, main.cooldownText);
            SetAnchors(cooldown.rectTransform, Vector2.zero, Vector2.one);
            cooldown.fontSize = row.FontSize(cooldown.transform, CooldownFontSize);
            cooldown.fontStyle = FontStyles.Bold;
            cooldown.alignment = TextAlignmentOptions.Center;
            cooldown.gameObject.AddComponent<CondensedText>();
            cooldown.gameObject.SetActive(false);

            Image arrow = NewObject(ArrowName, root).gameObject.AddComponent<Image>();
            arrow.sprite = FlatIcons.Down;
            arrow.color = Palette.HeaderText;
            arrow.raycastTarget = false;
            float arrowX = 1f - IconShare / 2f;
            float arrowY = 1f + (GapShare + IconShare * IconPaddingShare) / 2f;
            SetAnchors(arrow.rectTransform, new Vector2(arrowX - ArrowShare / 2f, arrowY - ArrowShare / 2f),
                new Vector2(arrowX + ArrowShare / 2f, arrowY + ArrowShare / 2f));
            AddKeys(root, row);
            deck = root.gameObject;
            deck.SetActive(false);
            recorder.Add(deck);

            // Over everything in the slot while it plays, its face hiding the slot's own icon and outline.
            RectTransform swapRect = NewObject(SwapName, slot);
            SetAnchors(swapRect, Vector2.zero, Vector2.one);
            Image face = swapRect.gameObject.AddComponent<Image>();
            face.color = Palette.Trough;
            face.raycastTarget = false;
            growing = NewObject("Next", swapRect).gameObject.AddComponent<RawImage>();
            growing.raycastTarget = false;
            swap = swapRect.gameObject;
            swap.SetActive(false);
            recorder.Add(swap);
        }

        public bool Alive => deck && main && alternate;

        // Read by the SevenUI.Tests plugin.
        public bool Swapping => swapStart >= 0f;

        // Every frame: a few reads per equipment held, and the icons moved while the swap plays.
        public void Update()
        {
            if (!Alive) return;
            Inventory now = main.targetInventory;
            EquipmentLocation at = now ? now.activeEquipmentLocation : default;
            bool swapped = now && now == inventory && (at.slot != location.slot || at.set != location.set);
            inventory = now;
            location = at;
            ReadQueue(now, at);
            if (swapped && lastNext)
            {
                StartSwap();
            }

            bool any = false;
            foreach (EquipmentIndex index in queue)
            {
                any |= index != EquipmentIndex.None;
            }
            bool shown = alternate.displayRoot && alternate.displayRoot.activeSelf && any;
            if (deck.activeSelf != shown)
            {
                deck.SetActive(shown);
            }
            // The keys switch a Coupler's sets; MUL-T's Retool switches his slots with a skill of its own.
            bool sets = now && now.accessibleEquipmentSetCount > 1;
            if (keys.activeSelf != sets)
            {
                keys.SetActive(sets);
            }
            // Inventory.GetEquipmentDisabled only returns a synced bool (the [Server] setter is SetEquipmentDisabled, never called here).
            bool disabled = now && (now.GetEquipmentDisabled() || now.inventoryDisabled);
            for (int i = 0; i < MaxShown; i++)
            {
                ShowIcon(i, disabled);
            }
            TMP_Text seconds = alternate.cooldownText;
            bool counting = seconds && seconds.gameObject.activeSelf && icons[0].texture;
            if (cooldown.gameObject.activeSelf != counting)
            {
                cooldown.gameObject.SetActive(counting);
            }
            if (counting && cooldown.text != seconds.text)
            {
                cooldown.text = seconds.text;
            }
            lastNext = icons[0].texture;
            Animate();
        }

        // In the order the swap reaches them: the active slot's other equipment sets, or with MUL-T's two slots and no
        // Coupler, the other slot's equipment, as the game's own slot shows it. Read only.
        private void ReadQueue(Inventory now, EquipmentLocation at)
        {
            queue.Clear();
            if (!now) return;
            int sets = now.accessibleEquipmentSetCount;
            if (sets > 1)
            {
                for (int k = 1; k < sets; k++)
                {
                    EquipmentLocation later = at;
                    later.set = (uint)((at.set + k) % sets);
                    queue.Add(now.GetEquipment(in later).equipmentIndex);
                }
            }
            else if (now.equipmentSlotCount > 1)
            {
                EquipmentLocation next = now.nextSlotEquipmentLocation;
                queue.Add(now.GetEquipment(in next).equipmentIndex);
            }
        }

        // The next icon copies the game's slot (which shows the disabled picture when equipment is disabled); the later
        // ones are the equipment's own icons, hidden while equipment is disabled.
        private void ShowIcon(int i, bool disabled)
        {
            RawImage icon = icons[i];
            Texture texture = null;
            Color color = Color.clear;
            if (i < queue.Count)
            {
                if (i == 0 && alternate.iconImage)
                {
                    texture = alternate.iconImage.texture;
                    color = alternate.iconImage.color;
                }
                else if (!disabled && queue[i] != EquipmentIndex.None)
                {
                    texture = EquipmentIcons.Get(queue[i]);
                    color = Color.white;
                }
            }
            if (!texture)
            {
                color = Color.clear;
            }
            color.a *= Fades[i];
            if (icon.texture != texture)
            {
                icon.texture = texture;
            }
            if (icon.color != color)
            {
                icon.color = color;
            }
        }

        private void StartSwap()
        {
            growing.texture = lastNext;
            growing.color = Color.white;
            swapCount = Mathf.Min(queue.Count, MaxShown);
            swapStart = Time.unscaledTime;
            swap.SetActive(true);
        }

        // The old next icon grows from its place into the slot; each icon in the deck comes down from the place above,
        // and the one at the back, the equipment that was in use, shrinks into it from the slot.
        private void Animate()
        {
            if (!Swapping) return;
            float progress = (Time.unscaledTime - swapStart) / SwapSeconds;
            if (progress >= 1f)
            {
                swapStart = -1f;
                swap.SetActive(false);
                for (int i = 0; i < MaxShown; i++)
                {
                    Place(icons[i].rectTransform, i);
                }
                return;
            }
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            IconBox(0, out Vector2 nextMin, out Vector2 nextMax);
            SetAnchors(growing.rectTransform, Vector2.Lerp(nextMin, Vector2.zero, eased), Vector2.Lerp(nextMax, Vector2.one, eased));
            for (int i = 0; i < MaxShown; i++)
            {
                IconBox(i, out Vector2 min, out Vector2 max);
                Vector2 fromMin = Vector2.zero;
                Vector2 fromMax = Vector2.one;
                if (i < swapCount - 1)
                {
                    IconBox(i + 1, out fromMin, out fromMax);
                }
                SetAnchors(icons[i].rectTransform, Vector2.Lerp(fromMin, min, eased), Vector2.Lerp(fromMax, max, eased));
            }
        }

        private static void Place(RectTransform icon, int i)
        {
            IconBox(i, out Vector2 min, out Vector2 max);
            SetAnchors(icon, min, max);
        }

        // The icon's place, in shares of the slot's side from its bottom left.
        private static void IconBox(int i, out Vector2 min, out Vector2 max)
        {
            float bottom = 1f + GapShare + StepShare * i;
            min = new Vector2(1f - IconShare, bottom);
            max = new Vector2(1f, bottom + IconShare);
        }

        // "E + Q": the game's binding display for Interact and Equipment, which shows the player's own keys or their
        // gamepad's buttons, with a plus between, right-aligned beside the next icon. In the skills' key font, which
        // the skill row's styling sizes and colors like the other keys.
        private void AddKeys(RectTransform root, SkillRow row)
        {
            RectTransform keys = NewObject(KeysName, root);
            this.keys = keys.gameObject;
            float middle = 1f + GapShare + IconShare / 2f;
            float right = 1f - IconShare - KeysGapShare;
            SetAnchors(keys, new Vector2(right - KeysWidthShare, middle), new Vector2(right, middle));
            HorizontalLayoutGroup layout = keys.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.spacing = row.ScalerLength(KeySpacing);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            InputBindingDisplayController slotKey = main.GetComponentInChildren<InputBindingDisplayController>(true);
            TMP_Text style = slotKey ? slotKey.GetComponent<TMP_Text>() : null;
            NewKey(keys, style, "Interact");
            NewLabel("Plus", keys, style).text = "+";
            NewKey(keys, style, "Equipment");
        }

        private static void NewKey(Transform parent, TMP_Text style, string action)
        {
            TMP_Text label = NewLabel(action, parent, style);
            // Added after the text, which it fills in, and named before it wakes: it looks its binding up as soon as it
            // is enabled. It brings its own event system locator.
            label.gameObject.SetActive(false);
            label.gameObject.AddComponent<InputBindingDisplayController>().actionName = action;
            label.gameObject.SetActive(true);
        }

        private static TMP_Text NewLabel(string name, Transform parent, TMP_Text style)
        {
            TextMeshProUGUI label = NewObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                label.font = style.font;
                label.fontSharedMaterial = style.fontSharedMaterial;
                label.fontSize = style.fontSize;
                label.fontStyle = style.fontStyle;
                label.characterSpacing = style.characterSpacing;
                label.color = style.color;
            }
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform NewObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }
    }
}
