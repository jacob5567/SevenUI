using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static SevenUI.PickerPanel;

namespace SevenUI
{
    // The styling pieces a picker panel's cards, slots, cancel button and layout share: the game's button restyle, the
    // tier strip, count and outline, and the text and image helpers, all recorded for the panel's restore.
    internal sealed class PickerUi
    {
        private readonly PickerPanel owner;

        public PickerUi(PickerPanel owner, PickupPickerPanel panel, StyleRecorder recorder, RectTransform juice, TMP_Text title)
        {
            this.owner = owner;
            Panel = panel;
            Recorder = recorder;
            Juice = juice;
            Title = title;
        }

        public PickupPickerPanel Panel { get; }

        public StyleRecorder Recorder { get; }

        public RectTransform Juice { get; }

        public TMP_Text Title { get; }

        public bool FullDescriptions { get; set; }

        // The game's button frame and outlines give way to a flat look; its hover outline, which the game fades in, to
        // the canvas's sharp one, shown while the game's would be.
        public PickerButton StyleButton(MPButton button, PickupDef pickup, Color face)
        {
            PickerButton styled = new PickerButton
            {
                Button = button, Hover = button as HGButton, Pickup = pickup,
                DescriptionText = PickupDescriptions.For(pickup, button, FullDescriptions),
            };
            styled.Parts = ItemStatsText.Split(styled.DescriptionText);
            // The game's skin sets the button's tint, which multiplies the face's color (the options' slots came out
            // black, not the item bar's grey), and puts its text style back every frame. The tint is left neutral; an
            // option that can't be picked keeps the game's.
            Recorder.SetEnabled(button.GetComponent<ButtonSkinController>(), false);
            // The hovered option's name and description show in its card or under the grid, so a tooltip over it
            // (LookingGlass gives each option one) would repeat them. A turned-off provider gets no pointer or
            // selection events; its text is still read for the description (PickupDescriptions).
            foreach (TooltipProvider tooltip in button.GetComponents<TooltipProvider>())
            {
                Recorder.SetEnabled(tooltip, false);
            }
            ColorBlock colors = button.colors;
            Recorder.OnRestore(() =>
            {
                if (button)
                {
                    button.colors = colors;
                }
            });
            ColorBlock flat = colors;
            flat.normalColor = flat.highlightedColor = flat.pressedColor = flat.selectedColor = Color.white;
            button.colors = flat;
            // The button's own image when its target graphic isn't set, as on the cursed book's cancel button.
            Image frame = button.image ? button.image : button.GetComponent<Image>();
            if (frame)
            {
                Recorder.Record(frame);
                frame.sprite = null;
                frame.type = Image.Type.Simple;
                frame.color = face;
                // The game's button skin gives the image a material of its own, which can draw the face darker than
                // its color (the cancel button's faint face came out near black), so the flat face uses the default
                // UI material.
                Material material = frame.material;
                if (material != frame.defaultMaterial)
                {
                    Recorder.OnRestore(() =>
                    {
                        if (frame)
                        {
                            frame.material = material;
                        }
                    });
                    frame.material = null;
                }
            }
            Transform baseOutline = button.transform.Find(BaseOutlineName);
            Recorder.Disable(baseOutline ? baseOutline.GetComponent<Image>() : null);
            Transform hoverOutline = button.transform.Find(HoverOutlineName);
            Recorder.Disable(hoverOutline ? hoverOutline.GetComponent<Image>() : null);
            // The game's button turns its 'image on interactable' back on every frame while it can be pressed, which
            // would bring back a frame hidden above, so the button loses the reference while the flat look is on.
            HGButton hg = button as HGButton;
            if (hg && hg.imageOnInteractable)
            {
                Image onInteractable = hg.imageOnInteractable;
                Recorder.OnRestore(() =>
                {
                    if (hg)
                    {
                        hg.imageOnInteractable = onInteractable;
                    }
                });
                hg.imageOnInteractable = null;
            }
            Transform icon = button.transform.Find(IconName);
            if (icon)
            {
                styled.Icon = (RectTransform)icon;
                Recorder.RecordRect(styled.Icon);
            }
            return styled;
        }

        public void AddTierStrip(RectTransform slot, PickupDef pickup)
        {
            RectTransform tierStrip = NewImage("TierStrip", slot, TierColor(pickup));
            tierStrip.anchorMin = Vector2.zero;
            tierStrip.anchorMax = new Vector2(1f, 0f);
            tierStrip.pivot = new Vector2(0.5f, 0f);
            tierStrip.sizeDelta = new Vector2(0f, TierStripHeight);
        }

        // Bottom right, just above the tier strip, as on the item bar's slots.
        public TMP_Text AddCount(RectTransform slot)
        {
            TMP_Text count = AddText("Count", slot, CountSize, Color.white);
            count.gameObject.AddComponent<CondensedText>();
            count.fontStyle = FontStyles.Bold;
            count.alignment = TextAlignmentOptions.BottomRight;
            count.margin = new Vector4(0f, 0f, CountInset, TierStripHeight);
            Stretch(count.rectTransform);
            return count;
        }

        public PixelOutline AddOutline(RectTransform holder) => AddOutline(holder, Palette.Accent, OutlineEdge);

        // An outline of this color and edge width over the holder, hidden until shown.
        public PixelOutline AddOutline(RectTransform holder, Color color, float edge)
        {
            PixelOutline outline = PixelOutline.Add(holder, "Outline", color, edge);
            Recorder.Add(outline.gameObject);
            return outline;
        }

        // In the game title's font, so the font swap gives it Barlow like the rest of the HUD.
        public TMP_Text AddText(string name, RectTransform holder, float size, Color color)
        {
            RectTransform rect = owner.NewObject(name, holder);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (Title)
            {
                text.font = Title.font;
                text.fontSharedMaterial = Title.fontSharedMaterial;
                text.spriteAsset = Title.spriteAsset;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            return text;
        }

        public StatsSection NewStats(Transform holder) =>
            new StatsSection(holder, Title, PickerPanel.Canvas, DescriptionSize, made => Recorder.Add(made));

        public RectTransform NewImage(string name, Transform holder, Color color)
        {
            RectTransform rect = owner.NewObject(name, holder);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        public static float Measure(TMP_Text text, float width) => text ? text.GetPreferredValues(text.text, width, 0f).y : 0f;

        public static Color TierColor(PickupDef pickup) => pickup != null ? pickup.baseColor : Palette.Text;

        // Across the full width of its parent's top, this tall.
        public static void AcrossTop(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
        }

        // Placed from its parent's top left corner, x across and y down.
        public static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
