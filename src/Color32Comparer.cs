using System.Collections.Generic;
using UnityEngine;

namespace SevenUI
{
    // Compares colors by their four bytes. Color32 has no equality of its own, so a dictionary keyed by it would
    // otherwise box each key and compare its fields by reflection.
    internal sealed class Color32Comparer : IEqualityComparer<Color32>
    {
        public bool Equals(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        public int GetHashCode(Color32 color) => (color.r << 24) | (color.g << 16) | (color.b << 8) | color.a;
    }
}
