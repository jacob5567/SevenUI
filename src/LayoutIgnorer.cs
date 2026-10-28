using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Keeps an object the mod adds out of its parent's layout group. A LayoutElement with ignoreLayout would do the
    // same, but LookingGlass takes the first LayoutElement under the objective panel as its stats text's, so the mod's
    // objects must not have one.
    internal sealed class LayoutIgnorer : MonoBehaviour, ILayoutIgnorer
    {
        public bool ignoreLayout => true;
    }
}
