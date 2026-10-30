using System;
using UnityEngine;

namespace SevenUI.Tests
{
    // One objective the tests put on the objective panel: its text, the tracker whose strip it is drawn on, and the
    // share a strip's progress bar is filled to.
    internal sealed class TestObjectiveSource : MonoBehaviour
    {
        public string Text;
        public Type Tracker;
        public float Progress;
    }
}
