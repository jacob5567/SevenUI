using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // One flat panel behind the interaction prompt, fitted to the text it shows, as on the design canvas. The game's
    // prompt rows are strips as wide as the screen's right column, which only look right as gradients fading out;
    // flat, they would run far past the text. The panel covers both rows (the action and Inspect), and follows the
    // text as it changes, since the prompt's words depend on what the player looks at.
    internal sealed class PromptPanel
    {
        public const string PanelName = "SevenUIPromptPanel";
        // Room around the text, in HUD canvas units: the design's 8px by 14px.
        private const float PaddingX = 14f;
        private const float PaddingY = 8f;

        private readonly ContextManager prompt;
        private readonly RectTransform parent;
        private readonly RectTransform panel;
        private readonly TMP_Text[] texts;
        private readonly string[] lastTexts;
        private bool lastContext;
        private bool lastInspect;

        public PromptPanel(ContextManager prompt, StyleRecorder recorder)
        {
            this.prompt = prompt;
            parent = (RectTransform)prompt.transform;
            texts = new TMP_Text[] { prompt.glyphTMP, prompt.descriptionTMP, prompt.inspectGlyphTMP, prompt.inspectDescriptionTMP };
            lastTexts = new string[texts.Length];

            GameObject created = new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(LayoutIgnorer));
            created.layer = prompt.gameObject.layer;
            panel = (RectTransform)created.transform;
            panel.SetParent(parent, false);
            // First, so the rows draw over it.
            panel.SetAsFirstSibling();
            panel.anchorMin = panel.anchorMax = parent.pivot;
            panel.pivot = new Vector2(0.5f, 0.5f);
            Image image = created.GetComponent<Image>();
            image.sprite = null;
            image.color = Palette.Panel;
            image.raycastTarget = false;
            created.SetActive(false);
            recorder.Add(created);
        }

        public bool Alive => prompt && panel;

        public ContextManager Prompt => prompt;

        public RectTransform Panel => panel;

        // Cheap when nothing changed: two active flags and four short strings, which the game builds anew every frame
        // while the player looks at something, so they are compared by value. The text is measured only after it
        // changes.
        public void Update()
        {
            if (!Alive) return;
            bool context = prompt.contextDisplay && prompt.contextDisplay.activeInHierarchy;
            bool inspect = prompt.inspectDisplay && prompt.inspectDisplay.activeInHierarchy;
            bool changed = context != lastContext || inspect != lastInspect;
            for (int i = 0; i < texts.Length; i++)
            {
                string text = texts[i] ? texts[i].text : null;
                if (!string.Equals(text, lastTexts[i], System.StringComparison.Ordinal))
                {
                    lastTexts[i] = text;
                    changed = true;
                }
            }
            if (!changed) return;
            lastContext = context;
            lastInspect = inspect;
            Fit();
        }

        private void Fit()
        {
            bool any = false;
            Bounds bounds = default;
            foreach (TMP_Text text in texts)
            {
                if (!text || !text.isActiveAndEnabled || string.IsNullOrEmpty(text.text)) continue;
                // The game set the text this frame, after the last mesh update.
                text.ForceMeshUpdate();
                Bounds local = text.textBounds;
                if (local.size.x <= 0f) continue;
                Vector3 min = parent.InverseTransformPoint(text.transform.TransformPoint(local.min));
                Vector3 max = parent.InverseTransformPoint(text.transform.TransformPoint(local.max));
                Bounds inParent = new Bounds(min, Vector3.zero);
                inParent.Encapsulate(max);
                if (any)
                {
                    bounds.Encapsulate(inParent);
                }
                else
                {
                    bounds = inParent;
                    any = true;
                }
            }

            panel.gameObject.SetActive(any);
            if (!any) return;
            panel.anchoredPosition = new Vector2(bounds.center.x, bounds.center.y);
            panel.sizeDelta = new Vector2(bounds.size.x + 2f * PaddingX, bounds.size.y + 2f * PaddingY);
        }
    }
}
