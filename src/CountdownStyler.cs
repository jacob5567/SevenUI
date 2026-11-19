using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using UnityEngine;

namespace SevenUI
{
    // Restyles the HUD's countdown panels (the moon's escape, a Winnowing trial, Champ Rush's doom timer) as flat panels
    // like the run info (CountdownPanel). The game adds each one under the top cluster when its countdown starts and
    // removes it when it ends, so a panel is found by its timer text there, styled once when it appears, and then
    // kept under the item bar and the boss bar, as the game makes the boss bar's room and the items change.
    internal sealed class CountdownStyler : IStyler
    {
        // Between the bars above and the panel, in the top cluster's units.
        private const float Gap = 8f;
        // Below this, in the cluster's units, a panel isn't moved, so it isn't dirtied every frame.
        private const float MoveTolerance = 0.1f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        // Every panel root looked at, styled or not, so each is looked at (and logged, if refused) once.
        private readonly HashSet<RectTransform> seen = new HashSet<RectTransform>();
        private readonly List<CountdownPanel> panels = new List<CountdownPanel>();
        private readonly List<TimerText> timers = new List<TimerText>();
        private readonly Vector3[] corners = new Vector3[4];

        public CountdownStyler(Transform root)
        {
            this.root = root;
        }

        public bool Enabled => SevenUIPlugin.Settings.FlatPanels.Value;

        // The tests read each styled panel's text and bar.
        public IReadOnlyList<CountdownPanel> Panels => panels;

        public void Apply()
        {
            Transform cluster = root.Find(HudPaths.TopCenterCluster);
            if (!cluster) return;
            cluster.GetComponentsInChildren(true, timers);
            foreach (TimerText timer in timers)
            {
                RectTransform panelRoot = RootOf(cluster, timer);
                if (!panelRoot || !seen.Add(panelRoot)) continue;
                string problem = Problem(panelRoot, timer);
                CountdownPanel panel = problem == null ? CountdownPanel.Style(panelRoot, timer, recorder) : null;
                if (panel == null)
                {
                    if (SevenUIPlugin.Settings.DebugLogging.Value)
                    {
                        SevenUIPlugin.Log.LogInfo($"Countdown '{panelRoot.name}' left as the game draws it: {problem ?? "no text for its timer"}");
                    }
                    continue;
                }
                panels.Add(panel);
            }
            timers.Clear();
        }

        public void Restore()
        {
            recorder.Restore();
            panels.Clear();
            seen.Clear();
        }

        public void Update()
        {
            seen.RemoveWhere(rect => !rect);
            panels.RemoveAll(panel => !panel.Alive);
            if (panels.Count == 0) return;
            RectTransform cluster = root.Find(HudPaths.TopCenterCluster) as RectTransform;
            if (!cluster) return;
            // The lowest edge of what the panels sit under, in the cluster's units.
            float bottom = BarsBottom(cluster);
            foreach (CountdownPanel panel in panels)
            {
                panel.Update();
                float wanted = -(cluster.rect.yMax - bottom) - Gap;
                RectTransform rect = panel.Root;
                if (Mathf.Abs(rect.anchoredPosition.y - wanted) > MoveTolerance || rect.anchoredPosition.x != 0f)
                {
                    rect.anchoredPosition = new Vector2(0f, wanted);
                }
                bottom = cluster.rect.yMax + wanted - CountdownPanel.Height;
            }
        }

        // The panel is the cluster's child holding the timer; a timer deeper down (another mod's, an infinite tower's
        // counter) with anything but the countdown around it is left alone.
        private static RectTransform RootOf(Transform cluster, TimerText timer)
        {
            Transform child = timer.transform;
            while (child && child.parent != cluster)
            {
                child = child.parent;
            }
            return child as RectTransform;
        }

        private static string Problem(RectTransform panelRoot, TimerText timer)
        {
            if (panelRoot.GetComponentsInChildren<TimerText>(true).Length != 1) return "more than one timer in it";
            if (panelRoot.GetComponentInChildren<InfiniteTowerTimeCounter>(true) ||
                panelRoot.GetComponentInParent<InfiniteTowerTimeCounter>()) return "an infinite tower's counter";
            if (panelRoot.GetComponentInChildren<ItemInventoryDisplay>(true) ||
                panelRoot.GetComponentInChildren<HUDBossHealthBarController>(true)) return "it holds the item or boss bar";
            return timer.targetLabel ? null : "its timer has no text";
        }

        // The cluster's top at the start, then the lower of the item bar and the boss bar's container where they show.
        private float BarsBottom(RectTransform cluster)
        {
            float bottom = cluster.rect.yMax;
            foreach (ItemInventoryDisplay items in cluster.GetComponentsInChildren<ItemInventoryDisplay>(false))
            {
                bottom = Mathf.Min(bottom, BottomOf(cluster, items.transform as RectTransform));
            }
            foreach (HUDBossHealthBarController boss in cluster.GetComponentsInChildren<HUDBossHealthBarController>(false))
            {
                if (boss.container && boss.container.activeInHierarchy)
                {
                    bottom = Mathf.Min(bottom, BottomOf(cluster, boss.container.transform as RectTransform));
                }
            }
            return bottom;
        }

        private float BottomOf(RectTransform cluster, RectTransform rect)
        {
            if (!rect) return float.MaxValue;
            rect.GetWorldCorners(corners);
            return Mathf.Min(cluster.InverseTransformPoint(corners[0]).y, cluster.InverseTransformPoint(corners[1]).y);
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled countdown values: {recorder.Count}, panels {panels.Count}");
            foreach (CountdownPanel panel in panels)
            {
                output.AppendLine($"  countdown '{panel.Root.name}': '{panel.ShownText}' of {panel.Total:0.#} s");
            }
        }
    }
}
