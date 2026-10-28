using System.Collections.Generic;
using UnityEngine;

namespace SevenUI
{
    // The game turns the HUD's corner clusters about 6 degrees about the vertical axis, so under the HUD camera's
    // perspective they lean in toward the middle of the screen; a few of their children are turned back or further
    // (the shared suffering counter, the Void Fiend corruption meter). This levels every such small turn so the whole
    // HUD faces the screen squarely. Larger turns, such as the equipment slot's 180-degree flip, and turns about other
    // axes are left alone.
    internal sealed class TiltFlattener
    {
        // The game's tilts are 6 and 15.5 degrees; anything this far or more is a deliberate turn, not a lean.
        private const float MaxTiltDegrees = 30f;
        // A quaternion's x and z parts below this mean the turn is about the vertical axis only.
        private const float AxisTolerance = 0.001f;
        private const float MinTiltDegrees = 0.01f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<Transform> scratch = new List<Transform>();

        public TiltFlattener(Transform root)
        {
            this.root = root;
        }

        public int LeveledCount => recorder.Count;

        public void Apply()
        {
            root.GetComponentsInChildren(true, scratch);
            foreach (Transform transform in scratch)
            {
                if (transform != root && IsLean(transform.localRotation))
                {
                    recorder.SetLocalRotation(transform, Quaternion.identity);
                }
            }
            scratch.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
        }

        private static bool IsLean(Quaternion rotation)
        {
            if (Mathf.Abs(rotation.x) > AxisTolerance || Mathf.Abs(rotation.z) > AxisTolerance) return false;
            float degrees = 2f * Mathf.Asin(Mathf.Min(1f, Mathf.Abs(rotation.y))) * Mathf.Rad2Deg;
            return degrees > MinTiltDegrees && degrees < MaxTiltDegrees;
        }
    }
}
