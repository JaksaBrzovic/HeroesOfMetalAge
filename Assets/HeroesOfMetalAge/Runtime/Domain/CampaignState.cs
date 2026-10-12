using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Runtime state of one campaign. Read-only to everyone outside the Domain; only <see cref="CampaignRules"/> changes it.
    /// </summary>
    public sealed class CampaignState
    {
        public const string PLAYER_OWNER_ID = "player";

        internal CampaignState()
        {
            Army = ArmyData.AsReadOnly();
            Garrison = GarrisonData.AsReadOnly();
            Inventory = InventoryData.AsReadOnly();
        }

        public IReadOnlyList<Troop> Army { get; }
        /// <summary>The player's home army in an owned town.</summary>
        public IReadOnlyList<Troop> Garrison { get; }
        /// <summary>Unequipped artefact IDs.</summary>
        public IReadOnlyList<string> Inventory { get; }
        public string HeroId { get; internal set; }
        public string MissionId { get; internal set; }
        public int Day { get; internal set; }
        public int Gold { get; internal set; }
        public int Experience { get; internal set; }
        public int Movement { get; internal set; }
        public WorldPosition Position { get; internal set; }
        public string TownOwnerId { get; internal set; }
        public bool IsTownOwned => TownOwnerId == PLAYER_OWNER_ID;
        /// <summary>True while the hero is stationed in the town's home slot instead of visiting.</summary>
        public bool IsHeroHome { get; internal set; }
        public bool HasRecruited { get; internal set; }
        public string WeaponId { get; internal set; }
        public string CharmId { get; internal set; }

        internal List<Troop> ArmyData { get; } = new List<Troop>();
        internal List<Troop> GarrisonData { get; } = new List<Troop>();
        internal List<string> InventoryData { get; } = new List<string>();
        internal HashSet<string> CompletedData { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal Dictionary<string, int> StockData { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Stock(string unitId)
        {
            return StockData.TryGetValue(unitId, out int count) ? count : 0;
        }

        public bool IsCompleted(string encounterId)
        {
            return CompletedData.Contains(encounterId);
        }

        /// <summary>Detached save record; ordered collections keep the JSON stable between saves.</summary>
        public CampaignSnapshot Snapshot()
        {
            return new CampaignSnapshot
            {
                version = CampaignSnapshot.CURRENT_VERSION,
                missionId = MissionId,
                heroId = HeroId,
                day = Day,
                gold = Gold,
                experience = Experience,
                movement = Movement,
                x = Position.X,
                y = Position.Y,
                townOwnerId = TownOwnerId,
                heroHome = IsHeroHome,
                recruited = HasRecruited,
                weaponId = WeaponId,
                charmId = CharmId,
                army = ToStacks(Army),
                garrison = ToStacks(Garrison),
                inventory = new List<string>(InventoryData),
                completed = CompletedData.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                stock = StockData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new StockSnapshot { unitId = pair.Key, count = pair.Value })
                    .ToList()
            };
        }

        private static List<StackSnapshot> ToStacks(IReadOnlyList<Troop> troops)
        {
            List<StackSnapshot> stacks = new List<StackSnapshot>(troops.Count);
            foreach (Troop troop in troops)
            {
                stacks.Add(new StackSnapshot { unitId = troop.UnitId, count = troop.Count });
            }

            return stacks;
        }
    }
}
