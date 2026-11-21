using System.Collections.Generic;
using RoR2;

namespace SevenUI
{
    // The name and drone tier an ally is grouped by, shared by the ally cards (AllyCardsStyler) and the scoreboard's
    // allies (ScoreboardAllies), so both group the same allies into the same kinds: allies of one name, drone tier and
    // leader are one kind. Each ally's name is worked out again only when its body or tier changes.
    internal sealed class AllyNames
    {
        private readonly Dictionary<CharacterMaster, (CharacterBody Body, int Tier, string Name)> names =
            new Dictionary<CharacterMaster, (CharacterBody, int, string)>();
        private readonly List<CharacterMaster> goneMasters = new List<CharacterMaster>();

        // The name the game's card shows (Util.GetBestBodyName, as AllyCardController reads it). An upgrade adds hidden
        // items to the same body, so the tier is read from the synced inventory each time rather than assumed from the
        // body.
        // A master whose body died keeps its last name: the game destroys a dead ally's master a second after its body
        // goes, and until then it would otherwise stand alone, its own card showing the dead mark beside its kind's.
        // (lostBodyToDeath is in the master's synced flags, so a client sees it too.) Null for an ally between bodies.
        // Masters that are gone are forgotten once more than twice 'expected' names are kept.
        public string NameOf(CharacterMaster master, int expected, out int tier)
        {
            tier = 0;
            CharacterBody body = master.GetBody();
            names.TryGetValue(master, out (CharacterBody Body, int Tier, string Name) known);
            if (!body)
            {
                if (!master.lostBodyToDeath || known.Name == null) return null;
                tier = known.Tier;
                return known.Name;
            }
            tier = body.inventory ? body.inventory.GetItemCountEffective(DLC3Content.Items.DroneUpgradeHidden) : 0;
            if (known.Name != null && known.Body == body && known.Tier == tier) return known.Name;
            string name = Util.GetBestBodyName(body.gameObject);
            names[master] = (body, tier, name);
            if (names.Count > expected * 2)
            {
                ForgetGoneMasters();
            }
            return name;
        }

        public void Clear()
        {
            names.Clear();
        }

        private void ForgetGoneMasters()
        {
            foreach (CharacterMaster master in names.Keys)
            {
                if (!master)
                {
                    goneMasters.Add(master);
                }
            }
            foreach (CharacterMaster master in goneMasters)
            {
                names.Remove(master);
            }
            goneMasters.Clear();
        }
    }
}
