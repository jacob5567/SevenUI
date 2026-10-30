using TMPro;
using UnityEngine;

namespace SevenUI
{
    // A slot's charge count inside the slot's bottom-right corner, as the design canvas draws a slot's stock: its last
    // digit's advance 4px from the slot's right, and its baseline where the canvas's 18px line box, 3px up, puts it.
    // The game hangs the count off the slot at offsets of its own, the equipment's in front of the slot's plane, which
    // the HUD's perspective camera draws nearer the screen's middle, and TMP lines text up by the font's own metrics.
    // So the drawn digit is measured in the slot while the count shows, and the text moved onto the spot, in the
    // slot's plane, when they disagree.
    internal sealed class CornerStock
    {
        private const float RightInset = 4f;
        private const float BottomInset = 3f;
        // Barlow's ascent and descent (1 and 0.2 of the size), centered in a line box one size tall, put the baseline a
        // tenth of the size above the box's bottom.
        private const float BaselineShare = 0.1f;
        private const float Baseline = BottomInset + SkillSlotsStyler.StockFontSize * BaselineShare;
        // In canvas pixels.
        private const float Tolerance = 0.05f;

        private readonly TMP_Text stock;
        private readonly RectTransform slot;
        private readonly SkillRow row;

        public CornerStock(TMP_Text stock, RectTransform slot, SkillRow row, StyleRecorder recorder)
        {
            this.stock = stock;
            this.slot = slot;
            this.row = row;
            recorder.Record(stock);
            recorder.RecordRect(stock.rectTransform);
            // The rect's record puts back its place in the plane, not its depth.
            Transform text = stock.transform;
            float depth = text.localPosition.z;
            recorder.OnRestore(() =>
            {
                if (text)
                {
                    Vector3 position = text.localPosition;
                    text.localPosition = new Vector3(position.x, position.y, depth);
                }
            });
            // Grows left and up from the corner as the count gains digits.
            stock.alignment = TextAlignmentOptions.BottomRight;
            stock.rectTransform.pivot = new Vector2(1f, 0f);
        }

        public bool Alive => stock && slot;

        public TMP_Text Stock => stock;

        // A few transforms of points while the count shows. TMP measures the text as the canvas draws it, so after a
        // change the count is placed from the next frame's measure.
        public void Update()
        {
            if (!Alive || !stock.isActiveAndEnabled) return;
            TMP_TextInfo info = stock.textInfo;
            float perUnit = row.PixelsPer(slot);
            if (info == null || info.characterCount == 0 || perUnit <= 0f) return;
            TMP_CharacterInfo last = info.characterInfo[info.characterCount - 1];
            Rect box = slot.rect;
            Vector2 spot = new Vector2(box.xMax - RightInset / perUnit, box.yMin + Baseline / perUnit);
            Transform text = stock.transform;
            Vector3 drawn = slot.InverseTransformPoint(text.TransformPoint(new Vector3(last.xAdvance, last.baseLine, 0f)));
            Vector3 shift = new Vector3(spot.x - drawn.x, spot.y - drawn.y, -drawn.z);
            if (shift.magnitude * perUnit < Tolerance) return;
            Vector3 at = slot.InverseTransformPoint(text.position);
            text.localPosition = text.parent.InverseTransformPoint(slot.TransformPoint(at + shift));
        }
    }
}
