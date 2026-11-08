using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // The next equipment, the one a Functional Coupler's swap (Interact + Equipment) or MUL-T's Retool switches to, as
    // a small badge centered on the equipment slot's top-left corner. The game draws it in a slot of its own right of
    // the equipment slot, which the tight margins put off the screen. That slot keeps running, invisible, and the badge
    // copies its icon, its grey while recharging and its cooldown, so the game decides what it shows. When the active
    // equipment changes, the two icons trade places: the new one grows from the badge into the slot while the old one
    // shrinks into the badge.
    internal sealed class EquipmentBadge
    {
        public const string Name = "SevenUIEquipmentBadge";
        public const string SwapName = "SevenUIEquipmentSwap";
        // Shares of the slot's side: the badge (40px beside the canvas's 90px slot at 1440p) and its dark rim.
        private const float Share = 0.44f;
        private const float RimShare = 0.035f;
        // In the design canvas's pixels.
        private const float CooldownFontSize = 16f;
        public const float SwapSeconds = 0.25f;

        private static readonly Vector2 BadgeMin = new Vector2(-Share / 2f, 1f - Share / 2f);
        private static readonly Vector2 BadgeMax = new Vector2(Share / 2f, 1f + Share / 2f);
        private static readonly Vector2 IconMin = BadgeMin + Vector2.one * RimShare;
        private static readonly Vector2 IconMax = BadgeMax - Vector2.one * RimShare;

        private readonly EquipmentIcon main;
        private readonly EquipmentIcon alternate;
        private readonly GameObject badge;
        private readonly RawImage icon;
        private readonly TMP_Text cooldown;
        private readonly GameObject swap;
        private readonly RawImage growing;
        private readonly RawImage shrinking;
        private Inventory inventory;
        private EquipmentLocation location;
        // Both slots' icons as last drawn, before a swap changes them.
        private Texture lastMain;
        private Texture lastNext;
        private float swapStart = -1f;

        public EquipmentBadge(EquipmentIcon main, EquipmentIcon alternate, RectTransform slot, SkillRow row, StyleRecorder recorder)
        {
            this.main = main;
            this.alternate = alternate;
            CanvasGroup hidden = alternate.gameObject.AddComponent<CanvasGroup>();
            hidden.alpha = 0f;
            hidden.interactable = false;
            hidden.blocksRaycasts = false;
            recorder.Add(hidden);

            RectTransform rim = NewObject(Name, slot, Palette.BadgeRim);
            SetAnchors(rim, BadgeMin, BadgeMax);
            RectTransform face = NewObject("Face", rim, Palette.Trough);
            SetAnchors(face, Vector2.one * (RimShare / Share), Vector2.one * (1f - RimShare / Share));
            icon = NewIcon("Icon", face);
            SetAnchors(icon.rectTransform, Vector2.zero, Vector2.one);
            cooldown = NewLabel(face, main.cooldownText, row);
            badge = rim.gameObject;
            recorder.Add(badge);

            // Over everything in the slot while it plays, its face hiding the slot's own icon and outline.
            RectTransform swapRect = NewObject(SwapName, slot, Palette.Trough);
            SetAnchors(swapRect, Vector2.zero, Vector2.one);
            shrinking = NewIcon("Old", swapRect);
            growing = NewIcon("New", swapRect);
            swap = swapRect.gameObject;
            swap.SetActive(false);
            recorder.Add(swap);
        }

        public bool Alive => badge && main && alternate;

        // Read by the SevenUI.Tests plugin.
        public bool Swapping => swapStart >= 0f;

        // Every frame: a few reads, and the swap's two icons moved while it plays.
        public void Update()
        {
            if (!Alive) return;
            Inventory now = main.targetInventory;
            EquipmentLocation at = now ? now.activeEquipmentLocation : default;
            if (now && now == inventory && (at.slot != location.slot || at.set != location.set) && (lastMain || lastNext))
            {
                StartSwap();
            }
            inventory = now;
            location = at;

            bool shown = alternate.displayRoot && alternate.displayRoot.activeSelf && !Swapping;
            if (badge.activeSelf != shown)
            {
                badge.SetActive(shown);
            }
            RawImage next = alternate.iconImage;
            if (next)
            {
                Copy(next, icon);
            }
            TMP_Text seconds = alternate.cooldownText;
            bool counting = seconds && seconds.gameObject.activeSelf;
            if (cooldown.gameObject.activeSelf != counting)
            {
                cooldown.gameObject.SetActive(counting);
            }
            if (counting && cooldown.text != seconds.text)
            {
                cooldown.text = seconds.text;
            }
            lastMain = main.iconImage ? main.iconImage.texture : null;
            lastNext = next ? next.texture : null;
            Animate();
        }

        // The icons from before the change: the next one becomes the slot's, the slot's the next.
        private void StartSwap()
        {
            growing.texture = lastNext;
            growing.color = lastNext ? Color.white : Color.clear;
            shrinking.texture = lastMain;
            shrinking.color = lastMain ? Color.white : Color.clear;
            swapStart = Time.unscaledTime;
            swap.SetActive(true);
            badge.SetActive(false);
        }

        private void Animate()
        {
            if (!Swapping) return;
            float progress = (Time.unscaledTime - swapStart) / SwapSeconds;
            if (progress >= 1f)
            {
                swapStart = -1f;
                swap.SetActive(false);
                return;
            }
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            SetAnchors(growing.rectTransform, Vector2.Lerp(IconMin, Vector2.zero, eased), Vector2.Lerp(IconMax, Vector2.one, eased));
            SetAnchors(shrinking.rectTransform, Vector2.Lerp(Vector2.zero, IconMin, eased), Vector2.Lerp(Vector2.one, IconMax, eased));
        }

        private static void Copy(RawImage from, RawImage to)
        {
            if (to.texture != from.texture)
            {
                to.texture = from.texture;
            }
            if (to.uvRect != from.uvRect)
            {
                to.uvRect = from.uvRect;
            }
            if (to.color != from.color)
            {
                to.color = from.color;
            }
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static RectTransform NewObject(string name, Transform parent, Color color)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            created.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            Image image = created.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RawImage NewIcon(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            RawImage image = created.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.color = Color.clear;
            return image;
        }

        // In the slot's cooldown's font, so the font swap gives it Barlow Condensed like the slot's own number.
        private static TMP_Text NewLabel(Transform parent, TMP_Text style, SkillRow row)
        {
            GameObject created = new GameObject("Cooldown", typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)created.transform;
            rect.SetParent(parent, false);
            SetAnchors(rect, Vector2.zero, Vector2.one);
            TextMeshProUGUI label = created.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                label.font = style.font;
                label.fontSharedMaterial = style.fontSharedMaterial;
            }
            label.fontSize = row.FontSize(rect, CooldownFontSize);
            label.fontStyle = FontStyles.Bold;
            label.color = Palette.Text;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            created.AddComponent<CondensedText>();
            created.SetActive(false);
            return label;
        }
    }
}
