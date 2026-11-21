using System.Collections.Generic;
using RoR2;
using RoR2.Skills;
using RoR2.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SevenUI
{
    // Each player's drones and other allies on the scoreboard, as on the design canvas's option F. Under each row's
    // name block a summary line (ScoreboardAlliesSummary): the portraits of up to four of the player's ally kinds
    // stacked, then how many allies they have. Clicking it opens that player's allies in a panel under the board,
    // centered at the inspect panel's share of the board's width: a header with the player, then a tile per kind with
    // its portrait, name, count and a health segment per ally. Clicking it again, or closing the scoreboard, closes
    // the panel. Kinds are grouped as the ally cards group them (AllyNames): one name, drone tier and leader.
    // Hovering a drone's tile shows the tooltip the game's notification shows when the drone is bought: its name and
    // function, and for an Operator with ADMIN-OVERRIDE what its command does (Tooltip).
    //
    // A player's allies are the minions in their minion group (MinionOwnership, synced, so a client sees every
    // player's), and those minions' own minions, alive. Read only. The board (ScoreboardItemsBoard) updates this while
    // it shows, and keeps the rows' room for the summary line while any player has an ally.
    internal sealed class ScoreboardAllies
    {
        public const string AreaName = "SevenUIAlliesArea";
        public const string PanelName = "SevenUIAlliesPanel";
        public const string TileName = "SevenUIAllyTile";

        private const float Canvas = HudUnits.PerCanvasPixel;

        // The canvas's sizes, in its pixels, scaled to the HUD's units (HudUnits).
        private const float BoardGap = 6f * Canvas;
        private const float StripHeight = 2f * Canvas;
        private const float HeaderHeight = 30f * Canvas;
        private const float HeaderPortrait = 20f * Canvas;
        private const float HeaderGap = 10f * Canvas;
        private const float HeaderNoteSize = 12f * Canvas;
        private const float PaddingX = 14f * Canvas;
        private const float PaddingY = 10f * Canvas;
        private const float TileMinWidth = 170f * Canvas;
        private const float TileHeight = 48f * Canvas;
        private const float TileGap = 6f * Canvas;
        private const float TilePortrait = 36f * Canvas;
        private const float TileInset = 6f * Canvas;
        private const float TileTextGap = 8f * Canvas;
        private const float TileNameSize = 13f * Canvas;
        private const float TileNameTop = 7f * Canvas;
        private const float BarTop = 30f * Canvas;
        private const float BarHeight = 5f * Canvas;
        private const float SegmentGap = 2f * Canvas;
        // More allies of a kind than this share one bar, their average health, as the segments would be too thin.
        private const int MaxSegments = 12;
        // How far up a chain of minion owners allies are looked for (a minion's minion, and so on).
        private const int MaxDepth = 8;
        private const string Times = "×";
        // The game's own check (CharacterMasterNotificationQueue.PushDroneNotification) for the drone notification's
        // command line: an Operator (DroneTechBody) whose secondary is this skill, ADMIN-OVERRIDE.
        private const string CommandSkillName = "Command: Activate";
        private const string CommandToken = "DRONETECH_COMMAND";
        private const string CommandSeparator = "\n\n";
        private static readonly Color TooltipBodyColor = Color.gray;
        // The drone notification's text colors (DroneRepairNotificationPanel): its description's light blue, and the
        // command's green, which the mod's restyled notification keeps (PromptsStyler). Restyled, the description is
        // the mod's notification description color instead.
        private static readonly Color NotificationDescription = new Color(0.824f, 0.949f, 1f, 1f);
        private static readonly Color NotificationCommand = new Color(0f, 0.991f, 0.698f, 1f);
        private static readonly Color TileFace = new Color(1f, 1f, 1f, 0.04f);

        // A kind of ally one player has: its members alive, in the order the minion groups list them.
        public sealed class Kind
        {
            public string Name;
            public int Tier;
            public CharacterMaster Leader;
            public Texture Portrait;
            // The drone's def, null for an ally that isn't a drone (a turret, a Beetle Guard).
            public DroneDef Drone;
            public readonly List<CharacterMaster> Members = new List<CharacterMaster>();
        }

        // One player's allies, gathered again every update into pooled kinds; Shape changes only when what is drawn
        // from them (the kinds, their portraits and counts) does.
        public sealed class PlayerAllies
        {
            public CharacterMaster Player;
            public readonly List<Kind> Kinds = new List<Kind>();
            public int Total;
            public int Shape;
            // Whether the player is an Operator who commands drones, so their tooltips add the command; kept from the
            // last body while the player has none.
            public bool Commands;
        }

        private sealed class Tile
        {
            public RectTransform Rect;
            public RawImage Portrait;
            public TMP_Text Label;
            public TooltipProvider Tooltip;
            public RectTransform Bar;
            public readonly List<RectTransform> Fills = new List<RectTransform>();
            public readonly List<RectTransform> Segments = new List<RectTransform>();
        }

        private readonly StyleRecorder recorder;
        private readonly RectTransform strips;
        private readonly TMP_Text style;
        private readonly AllyNames names = new AllyNames();
        private readonly Dictionary<CharacterMaster, PlayerAllies> players = new Dictionary<CharacterMaster, PlayerAllies>();
        private readonly Dictionary<ScoreboardStrip, ScoreboardAlliesSummary> summaries =
            new Dictionary<ScoreboardStrip, ScoreboardAlliesSummary>();
        private readonly Dictionary<MinionOwnership, CharacterMaster> minionMasters = new Dictionary<MinionOwnership, CharacterMaster>();
        private readonly Stack<Kind> spareKinds = new Stack<Kind>();
        private readonly List<CharacterMaster> gone = new List<CharacterMaster>();
        private readonly List<ScoreboardStrip> goneRows = new List<ScoreboardStrip>();
        private readonly List<Tile> tiles = new List<Tile>();
        private RectTransform area;
        private LayoutElement areaElement;
        private RectTransform face;
        private RawImage headerPortrait;
        private TMP_Text headerLabel;
        private TMP_Text headerNote;
        private int tileCount;
        private int laidOutShape;
        private float laidOutWidth = -1f;
        private TMP_FontAsset laidOutFont;
        private float faceHeight;
        private int counted;
        private bool laidOutRestyled;
        private static int commandSkill = -1;

        public ScoreboardAllies(StyleRecorder recorder, RectTransform strips, TMP_Text style)
        {
            this.recorder = recorder;
            this.strips = strips;
            this.style = style;
        }

        // Whether the rows keep room for the summary line: the setting on and any player on the board with an ally.
        public bool LineShown { get; private set; }

        // The player whose allies the panel shows, or none while it is closed.
        public CharacterMaster Open { get; private set; }

        // Read by the SevenUI.Tests plugin.
        public RectTransform Panel => face;
        public PlayerAllies AlliesOf(CharacterMaster player) =>
            player && players.TryGetValue(player, out PlayerAllies allies) ? allies : null;
        public ScoreboardAlliesSummary SummaryOf(ScoreboardStrip strip) =>
            strip && summaries.TryGetValue(strip, out ScoreboardAlliesSummary summary) ? summary : null;
        public int TileCount => tileCount;
        public string TileText(int index) => tiles[index].Label.text;
        public string HeaderText => headerLabel ? headerLabel.text : "";
        public float SegmentFill(int tile, int segment) => tiles[tile].Fills[segment].anchorMax.x;
        public TooltipProvider TileTooltip(int index) => tiles[index].Tooltip;

        // Opens the panel on this player's allies, or closes it if it shows them already.
        public void Toggle(CharacterMaster player)
        {
            Open = Open == player ? null : player;
            laidOutShape = 0;
        }

        public void Close()
        {
            Open = null;
        }

        // Every update while the board shows: each row's player's allies gathered again, and the summaries and the panel
        // drawn from them. True when the rows' room for the summary line changed.
        public bool Update(IReadOnlyList<ScoreboardItemGrid> grids)
        {
            bool on = SevenUIPlugin.Settings.ScoreboardAllies.Value;
            bool any = false;
            counted = 0;
            foreach (ScoreboardItemGrid grid in grids)
            {
                CharacterMaster player = grid.Alive ? grid.Strip.master : null;
                if (!player) continue;
                if (!players.TryGetValue(player, out PlayerAllies allies))
                {
                    allies = new PlayerAllies { Player = player };
                    players[player] = allies;
                }
                if (on)
                {
                    Gather(allies);
                }
                else
                {
                    Release(allies);
                }
                any |= allies.Total > 0;
            }
            ForgetGone(grids);
            bool line = on && any;
            if (Open && (!line || !players.ContainsKey(Open) || players[Open].Total == 0))
            {
                Open = null;
            }
            foreach (ScoreboardItemGrid grid in grids)
            {
                if (!grid.Alive) continue;
                ScoreboardAlliesSummary summary = Summary(grid.Strip);
                if (!summary) continue;
                PlayerAllies allies = AlliesOf(grid.Strip.master);
                summary.Show(line ? allies : null, line, allies != null && Open == allies.Player);
            }
            bool changed = line != LineShown;
            LineShown = line;
            return changed;
        }

        // The panel under the board, laid out again when its player's kinds, the board's width or the font changed,
        // and its health bars every update.
        public void Place()
        {
            PlayerAllies allies = AlliesOf(Open);
            bool show = allies != null && allies.Total > 0;
            if (show && !area)
            {
                Build();
            }
            if (!area) return;
            if (area.gameObject.activeSelf != show)
            {
                area.gameObject.SetActive(show);
            }
            if (!show) return;
            float width = Mathf.Round(strips.rect.width * ScoreboardInspect.WidthShare);
            int shape = allies.Shape ^ allies.Player.GetInstanceID();
            bool restyled = Restyled();
            if (shape != laidOutShape || !Mathf.Approximately(width, laidOutWidth) || headerLabel.font != laidOutFont ||
                restyled != laidOutRestyled)
            {
                laidOutShape = shape;
                laidOutRestyled = restyled;
                laidOutWidth = width;
                laidOutFont = headerLabel.font;
                Fill(allies);
                faceHeight = Layout(allies, width);
                float height = BoardGap + faceHeight;
                areaElement.minHeight = height;
                areaElement.preferredHeight = height;
                area.sizeDelta = new Vector2(area.sizeDelta.x, height);
                face.anchorMin = face.anchorMax = face.pivot = new Vector2(0.5f, 1f);
                face.anchoredPosition = new Vector2(0f, -BoardGap);
                face.sizeDelta = new Vector2(width, faceHeight);
            }
            for (int i = 0; i < tileCount && i < allies.Kinds.Count; i++)
            {
                ShowHealth(tiles[i], allies.Kinds[i]);
            }
        }

        // The player's allies, down their chains of minions, into pooled kinds.
        private void Gather(PlayerAllies allies)
        {
            Release(allies);
            Walk(allies, allies.Player, 0);
            CharacterBody player = allies.Player.GetBody();
            if (player)
            {
                allies.Commands = Commands(player);
            }
            int shape = allies.Commands ? 19 : 17;
            foreach (Kind kind in allies.Kinds)
            {
                allies.Total += kind.Members.Count;
                shape = shape * 31 + kind.Name.GetHashCode();
                shape = shape * 31 + kind.Tier;
                shape = shape * 31 + (kind.Leader ? kind.Leader.GetInstanceID() : 0);
                shape = shape * 31 + kind.Members.Count;
                shape = shape * 31 + (kind.Portrait ? kind.Portrait.GetInstanceID() : 0);
                shape = shape * 31 + (kind.Drone ? (int)kind.Drone.droneIndex : -1);
            }
            allies.Shape = shape;
        }

        // Reads the body's skills only; SkillCatalog.FindSkillIndexByName is a lookup, found once.
        private static bool Commands(CharacterBody body)
        {
            if (body.bodyIndex != DLC3Content.BodyPrefabs.DroneTechBody.bodyIndex) return false;
            GenericSkill secondary = body.skillLocator ? body.skillLocator.secondary : null;
            if (!secondary || !secondary.baseSkill) return false;
            if (commandSkill < 0)
            {
                commandSkill = SkillCatalog.FindSkillIndexByName(CommandSkillName);
            }
            return commandSkill >= 0 && secondary.baseSkill.skillIndex == commandSkill;
        }

        private void Walk(PlayerAllies allies, CharacterMaster leader, int depth)
        {
            if (depth >= MaxDepth) return;
            MinionOwnership.MinionGroup group = MinionOwnership.MinionGroup.FindGroup(leader.netId);
            if (group == null) return;
            MinionOwnership[] members = group.members;
            for (int i = 0; i < group.memberCount && i < members.Length; i++)
            {
                CharacterMaster master = MasterOf(members[i]);
                if (!master || master == allies.Player) continue;
                if (IsAlive(master))
                {
                    string name = names.NameOf(master, counted + 8, out int tier);
                    if (name != null)
                    {
                        counted++;
                        Add(allies, master, leader, name, tier);
                    }
                }
                Walk(allies, master, depth + 1);
            }
        }

        private CharacterMaster MasterOf(MinionOwnership minion)
        {
            if (!minion) return null;
            if (!minionMasters.TryGetValue(minion, out CharacterMaster master))
            {
                master = minion.GetComponent<CharacterMaster>();
                minionMasters[minion] = master;
            }
            return master;
        }

        private void Add(PlayerAllies allies, CharacterMaster master, CharacterMaster leader, string name, int tier)
        {
            Kind found = null;
            foreach (Kind kind in allies.Kinds)
            {
                if (kind.Name == name && kind.Tier == tier && kind.Leader == leader)
                {
                    found = kind;
                    break;
                }
            }
            if (found == null)
            {
                found = spareKinds.Count > 0 ? spareKinds.Pop() : new Kind();
                found.Name = name;
                found.Tier = tier;
                found.Leader = leader;
                CharacterBody body = master.GetBody();
                found.Portrait = body ? body.portraitIcon : null;
                allies.Kinds.Add(found);
            }
            // A member that just died has no body; the kind's drone is read from one that has.
            if (!found.Drone)
            {
                CharacterBody body = master.GetBody();
                found.Drone = body ? DroneCatalog.GetDroneDef(DroneCatalog.GetDroneIndexFromBodyIndex(body.bodyIndex)) : null;
            }
            found.Members.Add(master);
        }

        private void Release(PlayerAllies allies)
        {
            foreach (Kind kind in allies.Kinds)
            {
                kind.Members.Clear();
                kind.Leader = null;
                kind.Portrait = null;
                kind.Drone = null;
                kind.Name = null;
                spareKinds.Push(kind);
            }
            allies.Kinds.Clear();
            allies.Total = 0;
            allies.Shape = 0;
        }

        // Players and rows no longer on the board, and minions whose objects are gone.
        private void ForgetGone(IReadOnlyList<ScoreboardItemGrid> grids)
        {
            foreach (CharacterMaster player in players.Keys)
            {
                bool listed = false;
                foreach (ScoreboardItemGrid grid in grids)
                {
                    listed |= grid.Strip && grid.Strip.master == player;
                }
                if (!listed)
                {
                    gone.Add(player);
                }
            }
            foreach (CharacterMaster player in gone)
            {
                Release(players[player]);
                players.Remove(player);
            }
            gone.Clear();
            foreach (ScoreboardStrip row in summaries.Keys)
            {
                if (!row || !summaries[row])
                {
                    goneRows.Add(row);
                }
            }
            foreach (ScoreboardStrip row in goneRows)
            {
                summaries.Remove(row);
            }
            goneRows.Clear();
            if (minionMasters.Count > counted * 2 + 16)
            {
                minionMasters.Clear();
            }
        }

        private static bool IsAlive(CharacterMaster master)
        {
            CharacterBody body = master.GetBody();
            return body && body.healthComponent && body.healthComponent.alive;
        }

        // The row's summary line, made under its name block the first time the row is seen.
        private ScoreboardAlliesSummary Summary(ScoreboardStrip strip)
        {
            if (summaries.TryGetValue(strip, out ScoreboardAlliesSummary summary) && summary) return summary;
            Transform texts = strip.transform.Find(ScoreboardStyler.RowTextsPath);
            if (!texts || !strip.nameLabel) return null;
            summary = ScoreboardAlliesSummary.Add(texts, strip.nameLabel, () => Toggle(strip.master));
            recorder.Add(summary.gameObject);
            summaries[strip] = summary;
            return summary;
        }

        // Under the board, in the scoreboard's own column of parts, before the inspect panel.
        private void Build()
        {
            Transform container = strips.parent;
            area = UiFactory.NewObject(AreaName, container);
            area.SetSiblingIndex(strips.GetSiblingIndex() + 1);
            recorder.Add(area.gameObject);
            areaElement = area.gameObject.AddComponent<LayoutElement>();
            areaElement.minWidth = 0f;
            areaElement.preferredWidth = 0f;
            areaElement.flexibleWidth = 1f;
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 1f);
            area.pivot = new Vector2(0.5f, 1f);
            area.sizeDelta = new Vector2(strips.rect.width, 0f);

            face = UiFactory.NewImage(PanelName, area, Palette.ScoreboardPanel).rectTransform;
            Image strip = UiFactory.NewImage("Strip", face, Palette.Accent);
            TopRow(strip.rectTransform, 0f, StripHeight, 0f);
            Image header = UiFactory.NewImage("Header", face, Palette.HeaderStrip);
            TopRow(header.rectTransform, StripHeight, HeaderHeight, 0f);
            RectTransform slot = UiFactory.NewImage("PortraitSlot", header.transform, Palette.ItemSlot).rectTransform;
            StatsSection.TopLeft(slot, PaddingX, (HeaderHeight - HeaderPortrait) / 2f, HeaderPortrait, HeaderPortrait);
            headerPortrait = NewPortrait(slot);
            headerLabel = NewText("Label", header.transform, HeaderStyle.FontSize, Palette.HeaderText);
            HeaderStyle.Apply(headerLabel);
            headerLabel.alignment = TextAlignmentOptions.MidlineLeft;
            UiFactory.Stretch(headerLabel.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(PaddingX + HeaderPortrait + HeaderGap, 0f), new Vector2(-PaddingX, 0f));
            headerNote = NewText("Note", header.transform, HeaderNoteSize, Palette.HeaderSubtle);
            headerNote.alignment = TextAlignmentOptions.MidlineRight;
            UiFactory.Stretch(headerNote.rectTransform, Vector2.zero, Vector2.one, new Vector2(PaddingX, 0f),
                new Vector2(-PaddingX, 0f));
            area.gameObject.SetActive(false);
        }

        // The header and a tile per kind, with their texts; Layout places them.
        private void Fill(PlayerAllies allies)
        {
            CharacterMaster player = allies.Player;
            ScoreboardStrip row = RowOf(player);
            headerPortrait.texture = row && row.classIcon ? row.classIcon.texture : null;
            string name = row && row.nameLabel ? row.nameLabel.text : player.name;
            headerLabel.text = "Allies · " + name;
            headerNote.text = allies.Total + (allies.Total == 1 ? " ally" : " allies") + " · " + allies.Kinds.Count +
                              (allies.Kinds.Count == 1 ? " kind" : " kinds");
            tileCount = allies.Kinds.Count;
            while (tiles.Count < tileCount)
            {
                tiles.Add(NewTile());
            }
            for (int i = 0; i < tiles.Count; i++)
            {
                Tile tile = tiles[i];
                bool shown = i < tileCount;
                tile.Rect.gameObject.SetActive(shown);
                if (!shown) continue;
                Kind kind = allies.Kinds[i];
                tile.Portrait.texture = kind.Portrait;
                tile.Label.text = kind.Name + " <color=#" + ColorUtility.ToHtmlStringRGB(Palette.Accent) + ">" + Times +
                                  kind.Members.Count + "</color>";
                Segments(tile, kind.Members.Count > MaxSegments ? 1 : kind.Members.Count);
                Tooltip(tile.Tooltip, kind, allies.Commands, laidOutRestyled);
            }
        }

        // The drone notification's texts (GenericNotification.SetDrone): the name, here with its tier as the tile has
        // it, in the drone's color, its function, then for an Operator its command, which the notification shows in a
        // panel of its own. Each in the notification's color, in the look the tooltip has (restyled by TooltipStyler or
        // the game's). Without a title color the game shows no tooltip, so other allies have none.
        private static void Tooltip(TooltipProvider tooltip, Kind kind, bool commands, bool restyled)
        {
            DroneDef drone = kind.Drone;
            if (!drone)
            {
                tooltip.titleColor = Color.clear;
                tooltip.overrideTitleText = "";
                tooltip.overrideBodyText = "";
                return;
            }
            string body = Colored(Language.GetString(drone.pickupToken), restyled ? Palette.HeaderText : NotificationDescription);
            if (commands && !string.IsNullOrWhiteSpace(drone.droneTechCommandToken))
            {
                body += CommandSeparator + Colored(
                    Language.GetStringFormatted(CommandToken, Language.GetString(drone.droneTechCommandToken)), NotificationCommand);
            }
            tooltip.titleColor = ColorCatalog.GetColor(drone.color);
            tooltip.overrideTitleText = kind.Name;
            tooltip.bodyColor = TooltipBodyColor;
            tooltip.overrideBodyText = body;
        }

        private static string Colored(string text, Color color) =>
            "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + text + "</color>";

        // Whether TooltipStyler restyles tooltips, as it decides.
        private static bool Restyled() =>
            SevenUIPlugin.Settings.FlatPanels.Value && SevenUIPlugin.Settings.RestyleTooltips.Value;

        private ScoreboardStrip RowOf(CharacterMaster player)
        {
            foreach (ScoreboardStrip row in summaries.Keys)
            {
                if (row && row.master == player) return row;
            }
            return null;
        }

        // The face's height at this width: the strip, the header, then the tiles in as many columns as fit.
        private float Layout(PlayerAllies allies, float width)
        {
            float inner = Mathf.Max(0f, width - 2f * PaddingX);
            int columns = Mathf.Max(1, Mathf.FloorToInt((inner + TileGap) / (TileMinWidth + TileGap)));
            float tileWidth = (inner - (columns - 1) * TileGap) / columns;
            float top = StripHeight + HeaderHeight + PaddingY;
            for (int i = 0; i < tileCount; i++)
            {
                int column = i % columns;
                int line = i / columns;
                Tile tile = tiles[i];
                StatsSection.TopLeft(tile.Rect, PaddingX + column * (tileWidth + TileGap), top + line * (TileHeight + TileGap),
                    tileWidth, TileHeight);
                float textLeft = TileInset + TilePortrait + TileTextGap;
                float textWidth = Mathf.Max(0f, tileWidth - textLeft - TileInset);
                StatsSection.TopLeft(tile.Label.rectTransform, textLeft, TileNameTop, textWidth, TileNameSize * 1.25f);
                StatsSection.TopLeft(tile.Bar, textLeft, BarTop, textWidth, BarHeight);
                LayOutSegments(tile, textWidth);
            }
            int lines = (tileCount + columns - 1) / columns;
            return top + lines * TileHeight + Mathf.Max(0, lines - 1) * TileGap + PaddingY;
        }

        private Tile NewTile()
        {
            Image background = UiFactory.NewImage(TileName, face, TileFace);
            // The face takes the pointer, for the tooltip.
            background.raycastTarget = true;
            Tile tile = new Tile { Rect = background.rectTransform };
            tile.Tooltip = background.gameObject.AddComponent<TooltipProvider>();
            tile.Tooltip.titleColor = Color.clear;
            RectTransform slot = UiFactory.NewImage("PortraitSlot", tile.Rect, Palette.ItemSlot).rectTransform;
            StatsSection.TopLeft(slot, TileInset, (TileHeight - TilePortrait) / 2f, TilePortrait, TilePortrait);
            tile.Portrait = NewPortrait(slot);
            tile.Label = NewText("Label", tile.Rect, TileNameSize, Palette.Text);
            tile.Label.fontStyle = FontStyles.Bold;
            tile.Label.enableWordWrapping = false;
            tile.Label.overflowMode = TextOverflowModes.Ellipsis;
            tile.Bar = UiFactory.NewObject("Health", tile.Rect);
            return tile;
        }

        // This many segments in the tile's bar, each a trough with a fill.
        private static void Segments(Tile tile, int count)
        {
            while (tile.Segments.Count < count)
            {
                RectTransform segment = UiFactory.NewImage("Segment", tile.Bar, Palette.Trough).rectTransform;
                RectTransform fill = UiFactory.NewImage("Fill", segment, Palette.Health).rectTransform;
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = Vector2.one;
                fill.offsetMin = fill.offsetMax = Vector2.zero;
                tile.Segments.Add(segment);
                tile.Fills.Add(fill);
            }
            for (int i = 0; i < tile.Segments.Count; i++)
            {
                bool shown = i < count;
                if (tile.Segments[i].gameObject.activeSelf != shown)
                {
                    tile.Segments[i].gameObject.SetActive(shown);
                }
            }
        }

        private static void LayOutSegments(Tile tile, float width)
        {
            int count = 0;
            foreach (RectTransform segment in tile.Segments)
            {
                if (segment.gameObject.activeSelf) count++;
            }
            if (count == 0) return;
            float each = (width - (count - 1) * SegmentGap) / count;
            for (int i = 0; i < count; i++)
            {
                StatsSection.TopLeft(tile.Segments[i], i * (each + SegmentGap), 0f, each, BarHeight);
            }
        }

        // Each ally's health, or the kind's average once it has more allies than segments.
        private static void ShowHealth(Tile tile, Kind kind)
        {
            int members = kind.Members.Count;
            if (members > MaxSegments)
            {
                float sum = 0f;
                foreach (CharacterMaster member in kind.Members)
                {
                    sum += HealthOf(member);
                }
                SetFill(tile.Fills[0], sum / members);
                return;
            }
            for (int i = 0; i < members && i < tile.Fills.Count; i++)
            {
                SetFill(tile.Fills[i], HealthOf(kind.Members[i]));
            }
        }

        private static float HealthOf(CharacterMaster member)
        {
            CharacterBody body = member ? member.GetBody() : null;
            return body && body.healthComponent ? Mathf.Clamp01(body.healthComponent.combinedHealthFraction) : 0f;
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            if (!Mathf.Approximately(fill.anchorMax.x, fraction))
            {
                fill.anchorMax = new Vector2(fraction, 1f);
            }
        }

        private static RawImage NewPortrait(RectTransform slot)
        {
            RawImage portrait = UiFactory.NewObject("Portrait", slot).gameObject.AddComponent<RawImage>();
            portrait.raycastTarget = false;
            UiFactory.Stretch(portrait.rectTransform);
            return portrait;
        }

        private static void TopRow(RectTransform rect, float top, float height, float inset)
        {
            UiFactory.Stretch(rect, new Vector2(0f, 1f), Vector2.one, new Vector2(inset, -(top + height)),
                new Vector2(-inset, -top));
        }

        private TMP_Text NewText(string name, Transform parent, float size, Color color)
        {
            TextMeshProUGUI text = UiFactory.NewObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (style)
            {
                text.font = style.font;
                text.fontSharedMaterial = style.fontSharedMaterial;
            }
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
