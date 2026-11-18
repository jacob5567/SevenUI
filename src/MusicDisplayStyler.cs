using System.Text;
using BepInEx.Bootstrap;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the now-playing text of the MusicDisplay mod, when it is installed: Barlow, and a flat panel behind it
    // in the mod's panel color. MusicDisplay makes one text object on the local user's notification canvas, outside
    // the HUD, and fades its alpha; the panel is a sibling drawn under the text, sized to the text and faded with it.
    // Nothing of MusicDisplay is referenced, only the object's name.
    internal sealed class MusicDisplayStyler : IStyler
    {
        public const string PluginGuid = "com.DestroyedClone.MusicDisplay";
        public const string TextName = "MusicNameDisplay";
        public const string BackgroundName = "SevenUIMusicBackground";
        private const string GameFontPrefix = "tmpBomb";
        // Around the text, in the text's own units (the canvas's pixels at its scale).
        private const float PaddingX = 12f;
        private const float PaddingY = 6f;
        private const float HiddenAlpha = 0.01f;
        private const float HalfPivot = 0.5f;

        private TMP_Text text;
        private RectTransform background;
        private Image backgroundImage;
        private TMP_FontAsset originalFont;
        private Material originalMaterial;
        private bool originalLanguageFlag;
        private bool fontSwapped;

        public bool Enabled => Chainloader.PluginInfos.ContainsKey(PluginGuid) && SevenUIPlugin.Settings.FlatPanels.Value;

        // The text is found, and so styled, in Update: it is made by the other mod, in its own time.
        public void Apply()
        {
        }

        // Put the text's font back and take the panel out at once, as Destroy is deferred and Update would take the
        // leftover for a panel it has made.
        public void Restore()
        {
            RestoreFont();
            if (background)
            {
                background.SetParent(null, false);
                Object.DestroyImmediate(background.gameObject);
            }
            background = null;
            backgroundImage = null;
            text = null;
        }

        private void RestoreFont()
        {
            if (!fontSwapped || !text) return;
            text.font = originalFont;
            text.fontSharedMaterial = originalMaterial;
            HGTextMeshProUGUI languageText = text as HGTextMeshProUGUI;
            if (languageText)
            {
                languageText.useLanguageDefaultFont = originalLanguageFlag;
            }
            fontSwapped = false;
        }

        // Every frame: a lookup until the text is found, then a bounds read and a few assignments.
        public void Update()
        {
            if (!text || !background)
            {
                Restore();
                Find();
                if (!text) return;
                Style();
            }
            Follow();
        }

        private void Find()
        {
            LocalUser user = LocalUserManager.GetFirstLocalUser();
            Canvas canvas = user != null ? AchievementNotificationPanel.GetUserCanvas(user) : null;
            Transform found = canvas ? canvas.transform.Find(TextName) : null;
            text = found ? found.GetComponent<TMP_Text>() : null;
        }

        private void Style()
        {
            if (SevenUIPlugin.Settings.BarlowFont.Value)
            {
                SwapFont();
            }

            GameObject made = new GameObject(BackgroundName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            background = (RectTransform)made.transform;
            background.SetParent(text.transform.parent, false);
            // Under the text, not a child of it: a child draws over its parent.
            background.SetSiblingIndex(text.transform.GetSiblingIndex());
            background.pivot = new Vector2(HalfPivot, HalfPivot);
            backgroundImage = made.GetComponent<Image>();
            backgroundImage.raycastTarget = false;
            backgroundImage.color = Palette.Panel;
        }

        private void SwapFont()
        {
            TMP_FontAsset font = text.font;
            if (!font || !font.name.StartsWith(GameFontPrefix)) return;
            Material material = text.fontSharedMaterial;
            Material template = material ? material : font.material;
            TMP_FontAsset barlow = FontLibrary.Body(template);
            if (!barlow) return;

            originalFont = font;
            originalMaterial = material;
            HGTextMeshProUGUI languageText = text as HGTextMeshProUGUI;
            originalLanguageFlag = languageText && languageText.useLanguageDefaultFont;
            if (languageText)
            {
                languageText.useLanguageDefaultFont = false;
            }
            FontLibrary.AddFallback(barlow, font);
            text.font = barlow;
            // Setting the font resets the material to the font's own, so put the retargeted preset back after.
            text.fontSharedMaterial = FontLibrary.Retarget(template, barlow);
            fontSwapped = true;
        }

        // The text's fade and the track's length change what is drawn, and its config moves and scales it.
        private void Follow()
        {
            Transform notif = text.transform;
            bool shown = text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text) && text.alpha > HiddenAlpha;
            if (backgroundImage.enabled != shown)
            {
                backgroundImage.enabled = shown;
            }
            if (!shown) return;

            background.localScale = notif.localScale;
            background.localRotation = notif.localRotation;
            Bounds bounds = text.textBounds;
            background.position = notif.TransformPoint(bounds.center);
            background.sizeDelta = new Vector2(bounds.size.x + 2f * PaddingX, bounds.size.y + 2f * PaddingY);
            Color color = Palette.Panel;
            color.a *= text.alpha;
            backgroundImage.color = color;
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  music display: text {(text ? text.name : "not found")}, panel {(background ? "on" : "none")}");
        }
    }
}
