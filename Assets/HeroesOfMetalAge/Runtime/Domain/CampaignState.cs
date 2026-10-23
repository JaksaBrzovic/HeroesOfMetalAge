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
        public const int PRIMARY_STAT_COUNT = 4;

        internal CampaignState()
        {
            Army = ArmyData.AsReadOnly();
            Garrison = GarrisonData.AsReadOnly();
            Inventory = InventoryData.AsReadOnly();
            Skills = SkillData.AsReadOnly();
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
        /// <summary>Morale added by map objects until the next won battle.</summary>
        public int MoraleBonus { get; internal set; }
        /// <summary>Luck added by map objects until the next won battle.</summary>
        public int LuckBonus { get; internal set; }
        /// <summary>Fog of war: the cells the hero has seen.</summary>
        public ExploredMap Explored { get; internal set; }
        /// <summary>The secondary skills the hero knows, in the order learned.</summary>
        public IReadOnlyList<HeroSkill> Skills { get; }
        /// <summary>The highest level whose level-up has been granted (its primary stat rolled); from 1.</summary>
        public int LevelsGranted { get; internal set; } = 1;
        /// <summary>The level-up waiting for the player's choice of skill, or null.</summary>
        public LevelUpOffer PendingLevelUp { get; internal set; }
        /// <summary>Level-ups in a row that offered no Wisdom, for the class's Wisdom guarantee.</summary>
        public int LevelsWithoutWisdom { get; internal set; }
        public bool HasSpellBook { get; internal set; }
        /// <summary>Spell points left; regained every day.</summary>
        public int Mana { get; internal set; }

        internal List<Troop> ArmyData { get; } = new List<Troop>();
        internal List<Troop> GarrisonData { get; } = new List<Troop>();
        internal List<string> InventoryData { get; } = new List<string>();
        internal List<HeroSkill> SkillData { get; } = new List<HeroSkill>();
        /// <summary>Primary stats won on level-ups, per <see cref="PrimaryStat"/>.</summary>
        internal int[] PrimaryGainData { get; } = new int[PRIMARY_STAT_COUNT];
        /// <summary>The spells in the spell book.</summary>
        internal HashSet<string> SpellData { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> CompletedData { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal Dictionary<string, int> StockData { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>Artefact ID per <see cref="EquipmentPlace"/>, null where nothing is worn.</summary>
        internal string[] EquippedData { get; } = new string[Equipment.PLACE_COUNT];
        /// <summary>Map objects taken, opened or visited for their one-time effect.</summary>
        internal HashSet<string> VisitedData { get; } = new HashSet<string>(StringComparer.Ordinal);
        internal HashSet<string> KeyData { get; } = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>Creatures for sale per dwelling ID.</summary>
        internal Dictionary<string, int> DwellingStockData { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        /// <summary>The week each mill last paid out, per mill ID.</summary>
        internal Dictionary<string, int> MillWeekData { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public int Stock(string unitId)
        {
            return StockData.TryGetValue(unitId, out int count) ? count : 0;
        }

        public bool IsCompleted(string encounterId)
        {
            return CompletedData.Contains(encounterId);
        }

        /// <summary>The artefact worn in a place, or null.</summary>
        public string Equipped(EquipmentPlace place)
        {
            return EquippedData[(int)place];
        }

        public bool IsVisited(string objectId)
        {
            return VisitedData.Contains(objectId);
        }

        public bool HasKey(string keyId)
        {
            return keyId != null && KeyData.Contains(keyId);
        }

        public int DwellingStock(string objectId)
        {
            return DwellingStockData.TryGetValue(objectId, out int count) ? count : 0;
        }

        /// <summary>The week a mill last paid out, 0 if never.</summary>
        public int MillWeek(string objectId)
        {
            return MillWeekData.TryGetValue(objectId, out int week) ? week : 0;
        }

        /// <summary>How well the hero knows a secondary skill; <see cref="SkillLevel.None"/> when not at all.</summary>
        public SkillLevel SkillLevelOf(SecondarySkill skill)
        {
            foreach (HeroSkill known in SkillData)
            {
                if (known.Skill == skill)
                {
                    return known.Level;
                }
            }

            return SkillLevel.None;
        }

        /// <summary>A primary stat won on level-ups so far.</summary>
        public int PrimaryGain(PrimaryStat stat)
        {
            return PrimaryGainData[(int)stat];
        }

        public bool KnowsSpell(string spellId)
        {
            return spellId != null && SpellData.Contains(spellId);
        }

        /// <summary>True if the artefact is carried or worn.</summary>
        public bool Owns(string artefactId)
        {
            return artefactId != null && (InventoryData.Contains(artefactId) || Array.IndexOf(EquippedData, artefactId) >= 0);
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
                morale = MoraleBonus,
                luck = LuckBonus,
                explored = Explored.Encode(),
                equipped = EquippedData.Select(id => id ?? string.Empty).ToList(),
                visited = VisitedData.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                keys = KeyData.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                dwellings = ToCounts(DwellingStockData),
                mills = ToCounts(MillWeekData),
                army = ToStacks(Army),
                garrison = ToStacks(Garrison),
                inventory = new List<string>(InventoryData),
                completed = CompletedData.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                stock = StockData
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new StockSnapshot { unitId = pair.Key, count = pair.Value })
                    .ToList(),
                primaryGains = new List<int>(PrimaryGainData),
                skills = SkillData.Select(skill => new SkillSnapshot { skill = skill.Skill.ToString(), level = (int)skill.Level }).ToList(),
                levelsGranted = LevelsGranted,
                levelsWithoutWisdom = LevelsWithoutWisdom,
                pendingLevel = PendingLevelUp?.Level ?? 0,
                pendingStat = PendingLevelUp != null ? PendingLevelUp.Stat.ToString() : string.Empty,
                pendingSkills = PendingLevelUp != null
                    ? PendingLevelUp.Options.Select(option => new SkillSnapshot { skill = option.Skill.ToString(), level = (int)option.Level }).ToList()
                    : new List<SkillSnapshot>(),
                spellBook = HasSpellBook,
                spells = SpellData.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                mana = Mana
            };
        }

        private static List<CountSnapshot> ToCounts(Dictionary<string, int> counts)
        {
            return counts
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new CountSnapshot { id = pair.Key, count = pair.Value })
                .ToList();
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

    /// <summary>
    /// A level-up shown to the player: the primary stat it raised (already applied) and the secondary skills to choose from,
    /// each at the level it would reach. No options means there was nothing left to learn.
    /// </summary>
    public sealed class LevelUpOffer
    {
        public LevelUpOffer(int level, PrimaryStat stat, IEnumerable<HeroSkill> options)
        {
            Level = level;
            Stat = stat;
            Options = options.ToList().AsReadOnly();
        }

        public int Level { get; }
        public PrimaryStat Stat { get; }
        public IReadOnlyList<HeroSkill> Options { get; }
    }
}
