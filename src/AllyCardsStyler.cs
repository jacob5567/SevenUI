using System.Collections.Generic;
using System.Text;
using RoR2;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Restyles the ally cards on the left: each on a flat panel, with a flat health bar on the dark trough like the
    // player's. Allies of one kind (the same name, following the same leader) share one card, so a crowd of drones
    // doesn't run down over the chat: the first card of the kind stays, with "×8" beside its name and its bar split
    // into a segment per ally, each showing that ally's health, and the kind's other cards are hidden and take no room.
    // Players are never grouped. The game keeps adding, filling, sorting and removing the cards; this only hides some
    // and draws over the ones it keeps.
    internal sealed class AllyCardsStyler
    {
        private const string LeftClusterPath = "MainContainer/MainUIArea/SpringCanvas/LeftCluster";
        // Under each card's health bar.
        private const string TroughName = "BackgroundPanel";
        public const string SegmentsName = "SevenUIAllySegments";
        public const string CountName = "SevenUIAllyCount";

        // The design canvas's sizes, in its pixels, scaled to the HUD's units: 2px between segments, and the count 6px
        // after the name.
        private const float Canvas = HudUnits.PerCanvasPixel;
        private const float SegmentGap = 2f * Canvas;
        private const float CountGap = 6f * Canvas;
        // A segment turns red under this share of its ally's health, as the player's bar does.
        private const float LowHealthShare = 0.25f;

        private readonly Transform root;
        private readonly StyleRecorder recorder = new StyleRecorder();
        private readonly List<AllyCardController> cards = new List<AllyCardController>();
        private readonly List<Group> groups = new List<Group>();
        private readonly Stack<Group> spareGroups = new Stack<Group>();
        private readonly Dictionary<AllyCardController, LeadParts> leads = new Dictionary<AllyCardController, LeadParts>();
        // Cards hidden behind their kind's first card, with their own height, which the game's layout stacks them by.
        private readonly Dictionary<AllyCardController, float> hidden = new Dictionary<AllyCardController, float>();
        // Each ally's name as the game shows it, found once per body: a hidden card's own label stops updating.
        private readonly Dictionary<CharacterMaster, (CharacterBody Body, string Name)> names =
            new Dictionary<CharacterMaster, (CharacterBody, string)>();

        private sealed class Group
        {
            public AllyCardController Lead;
            public CharacterMaster Leader;
            public string Name;
            public readonly List<CharacterMaster> Members = new List<CharacterMaster>();
        }

        // What the mod adds to a kind's first card: the count after its name, and the segments in place of its bar.
        private sealed class LeadParts
        {
            public TMP_Text Count;
            public RectTransform Segments;
            public readonly List<Image> Fills = new List<Image>();
            public readonly List<GameObject> Slots = new List<GameObject>();
            public int ShownCount = -1;
            public string ShownName;
            public float[] Shown = new float[0];
        }

        public AllyCardsStyler(Transform root)
        {
            this.root = root;
        }

        public int HiddenCount => hidden.Count;

        public void Apply()
        {
            Transform cluster = root.Find(LeftClusterPath);
            if (!cluster) return;
            // In the game's order, top to bottom, so a kind's first card is the one it shows first.
            cluster.GetComponentsInChildren(true, cards);
            foreach (AllyCardController card in cards)
            {
                StyleCard(card);
            }
        }

        public void Restore()
        {
            foreach (KeyValuePair<AllyCardController, float> entry in hidden)
            {
                Show(entry.Key, entry.Value);
            }
            hidden.Clear();
            foreach (KeyValuePair<AllyCardController, LeadParts> entry in leads)
            {
                RemoveParts(entry.Key, entry.Value);
            }
            leads.Clear();
            cards.Clear();
            names.Clear();
            recorder.Restore();
        }

        // Every frame while the panels are on: the game reassigns its cards as allies come and go, and each ally's health
        // changes. A few reads per card, and a fill per segment when its health changed.
        public void Update()
        {
            cards.RemoveAll(card => !card);
            SortIntoKinds(cards);
            foreach (AllyCardController card in cards)
            {
                Group group = GroupOf(card);
                bool lead = group == null || group.Lead == card;
                if (lead && hidden.TryGetValue(card, out float height))
                {
                    hidden.Remove(card);
                    Show(card, height);
                }
                else if (!lead && !hidden.ContainsKey(card))
                {
                    hidden.Add(card, card.layoutElement ? card.layoutElement.preferredHeight : 0f);
                    Hide(card);
                }
                bool grouped = lead && group != null && group.Members.Count > 1;
                if (grouped)
                {
                    UpdateLead(card, group);
                }
                else if (leads.TryGetValue(card, out LeadParts parts))
                {
                    RemoveParts(card, parts);
                    leads.Remove(card);
                }
            }
        }

        // Sorts the assigned cards into kinds, in the cards' order.
        private void SortIntoKinds(List<AllyCardController> all)
        {
            foreach (Group group in groups)
            {
                group.Lead = null;
                group.Members.Clear();
                spareGroups.Push(group);
            }
            groups.Clear();
            foreach (AllyCardController card in all)
            {
                CharacterMaster master = card.sourceMaster;
                // Players stand alone, as does an ally between bodies, which has no name to group by.
                string name = master && !master.playerCharacterMasterController ? NameOf(master) : null;
                if (name == null) continue;
                CharacterMaster leader = master.minionOwnership ? master.minionOwnership.ownerMaster : null;
                Group kind = null;
                foreach (Group group in groups)
                {
                    if (group.Leader == leader && group.Name == name)
                    {
                        kind = group;
                        break;
                    }
                }
                if (kind == null)
                {
                    kind = spareGroups.Count > 0 ? spareGroups.Pop() : new Group();
                    kind.Lead = card;
                    kind.Leader = leader;
                    kind.Name = name;
                    groups.Add(kind);
                }
                kind.Members.Add(master);
            }
        }

        private Group GroupOf(AllyCardController card)
        {
            CharacterMaster master = card.sourceMaster;
            if (!master) return null;
            foreach (Group group in groups)
            {
                if (group.Members.Contains(master)) return group;
            }
            return null;
        }

        // The name the game's card shows (Util.GetBestBodyName, as AllyCardController reads it), found again only when
        // the ally's body changes.
        private string NameOf(CharacterMaster master)
        {
            CharacterBody body = master.GetBody();
            if (!body) return null;
            if (names.TryGetValue(master, out (CharacterBody Body, string Name) known) && known.Body == body) return known.Name;
            string name = Util.GetBestBodyName(body.gameObject);
            names[master] = (body, name);
            if (names.Count > cards.Count * 2)
            {
                ForgetGoneMasters();
            }
            return name;
        }

        private readonly List<CharacterMaster> gone = new List<CharacterMaster>();

        private void ForgetGoneMasters()
        {
            foreach (CharacterMaster master in names.Keys)
            {
                if (!master)
                {
                    gone.Add(master);
                }
            }
            foreach (CharacterMaster master in gone)
            {
                names.Remove(master);
            }
            gone.Clear();
        }

        // Turned off, and no height in the game's stack, so the cards under it close up.
        private static void Hide(AllyCardController card)
        {
            if (card.layoutElement)
            {
                card.layoutElement.preferredHeight = 0f;
            }
            card.gameObject.SetActive(false);
        }

        private static void Show(AllyCardController card, float height)
        {
            if (!card) return;
            if (card.layoutElement)
            {
                card.layoutElement.preferredHeight = height;
            }
            card.gameObject.SetActive(true);
        }

        private void UpdateLead(AllyCardController card, Group group)
        {
            if (!leads.TryGetValue(card, out LeadParts parts))
            {
                parts = AddParts(card);
                if (parts == null) return;
                leads.Add(card, parts);
            }
            int count = group.Members.Count;
            string name = card.nameLabel ? card.nameLabel.text : "";
            if (count != parts.ShownCount || name != parts.ShownName)
            {
                parts.ShownCount = count;
                parts.ShownName = name;
                parts.Count.text = "×" + count;
                float nameWidth = card.nameLabel ? card.nameLabel.GetPreferredValues(name).x : 0f;
                parts.Count.rectTransform.anchoredPosition = new Vector2(nameWidth + CountGap, 0f);
                SetSegmentCount(parts, count);
            }
            for (int i = 0; i < count; i++)
            {
                CharacterBody body = group.Members[i] ? group.Members[i].GetBody() : null;
                float share = body && body.healthComponent ? Mathf.Clamp01(body.healthComponent.combinedHealthFraction) : 0f;
                if (Mathf.Abs(share - parts.Shown[i]) < 0.001f) continue;
                parts.Shown[i] = share;
                RectTransform fill = parts.Fills[i].rectTransform;
                fill.anchorMax = new Vector2(share, 1f);
                parts.Fills[i].color = share < LowHealthShare ? Palette.LowHealth : Palette.Health;
            }
        }

        // The count as a child of the name, so it follows it; the segments in the bars' column where the health bar
        // was, as tall as it.
        private LeadParts AddParts(AllyCardController card)
        {
            if (!card.nameLabel || !card.healthBar) return null;
            LeadParts parts = new LeadParts();
            RectTransform count = NewObject(CountName, card.nameLabel.transform);
            count.anchorMin = count.anchorMax = new Vector2(0f, 0.5f);
            count.pivot = new Vector2(0f, 0.5f);
            count.sizeDelta = new Vector2(0f, card.nameLabel.rectTransform.rect.height);
            TextMeshProUGUI text = count.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = card.nameLabel.font;
            text.fontSharedMaterial = card.nameLabel.fontSharedMaterial;
            text.fontSize = card.nameLabel.fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = Palette.Accent;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            count.gameObject.AddComponent<CondensedText>();
            parts.Count = text;

            Transform bar = card.healthBar.transform;
            parts.Segments = NewObject(SegmentsName, bar.parent);
            parts.Segments.SetSiblingIndex(bar.GetSiblingIndex() + 1);
            LayoutElement barLayout = bar.GetComponent<LayoutElement>();
            LayoutElement layout = parts.Segments.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = barLayout ? barLayout.preferredHeight : ((RectTransform)bar).rect.height;
            HorizontalLayoutGroup row = parts.Segments.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = SegmentGap;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = true;
            bar.gameObject.SetActive(false);
            return parts;
        }

        private void SetSegmentCount(LeadParts parts, int count)
        {
            while (parts.Slots.Count < count)
            {
                RectTransform slot = NewObject("Segment", parts.Segments);
                Image trough = slot.gameObject.AddComponent<Image>();
                trough.color = Palette.Trough;
                trough.raycastTarget = false;
                slot.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                RectTransform fill = NewObject("Fill", slot);
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                Image fillImage = fill.gameObject.AddComponent<Image>();
                fillImage.color = Palette.Health;
                fillImage.raycastTarget = false;
                parts.Slots.Add(slot.gameObject);
                parts.Fills.Add(fillImage);
            }
            for (int i = 0; i < parts.Slots.Count; i++)
            {
                bool used = i < count;
                if (parts.Slots[i].activeSelf != used)
                {
                    parts.Slots[i].SetActive(used);
                }
            }
            // Unknown, so each fill is set on the next read.
            parts.Shown = new float[count];
            for (int i = 0; i < count; i++)
            {
                parts.Shown[i] = -1f;
            }
        }

        private static void RemoveParts(AllyCardController card, LeadParts parts)
        {
            if (parts.Count)
            {
                Object.Destroy(parts.Count.gameObject);
            }
            if (parts.Segments)
            {
                Object.Destroy(parts.Segments.gameObject);
            }
            if (card && card.healthBar)
            {
                card.healthBar.gameObject.SetActive(true);
            }
        }

        private static RectTransform NewObject(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            created.layer = parent.gameObject.layer;
            created.transform.SetParent(parent, false);
            return (RectTransform)created.transform;
        }

        private void StyleCard(AllyCardController card)
        {
            Image panel = card.GetComponent<Image>();
            if (panel)
            {
                recorder.Record(panel);
                panel.sprite = null;
                panel.type = Image.Type.Simple;
                panel.color = Palette.Panel;
            }
            if (!card.healthBar) return;
            FlatHealthStyles.Apply(card.healthBar, recorder);
            Transform trough = card.healthBar.transform.Find(TroughName);
            Image troughImage = trough ? trough.GetComponent<Image>() : null;
            if (troughImage)
            {
                recorder.Record(troughImage);
                troughImage.sprite = null;
                troughImage.type = Image.Type.Simple;
                troughImage.color = Palette.Trough;
            }
        }

        public void Describe(StringBuilder output)
        {
            output.AppendLine($"  restyled ally card values: {recorder.Count}");
            output.AppendLine($"  ally cards: {cards.Count}, grouped kinds: {leads.Count}, hidden behind them: {hidden.Count}");
        }
    }
}
