using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Tactical combat board size and damage tuning.</summary>
    public sealed class CombatPolicy
    {
        public CombatPolicy(int width, int height, int enemyAttack, int enemyDefense, double defenseMultiplier, double rangedMeleeMultiplier,
            double attackStep, double defenseStep, int maximumAttackDelta, int maximumDefenseDelta)
        {
            Width = width;
            Height = height;
            EnemyAttack = enemyAttack;
            EnemyDefense = enemyDefense;
            DefenseMultiplier = defenseMultiplier;
            RangedMeleeMultiplier = rangedMeleeMultiplier;
            AttackStep = attackStep;
            DefenseStep = defenseStep;
            MaximumAttackDelta = maximumAttackDelta;
            MaximumDefenseDelta = maximumDefenseDelta;
        }

        public int Width { get; }
        public int Height { get; }
        public int EnemyAttack { get; }
        public int EnemyDefense { get; }
        /// <summary>Damage multiplier for a defending target.</summary>
        public double DefenseMultiplier { get; }
        /// <summary>Damage multiplier when a ranged unit fights in melee.</summary>
        public double RangedMeleeMultiplier { get; }
        public double AttackStep { get; }
        public double DefenseStep { get; }
        public int MaximumAttackDelta { get; }
        public int MaximumDefenseDelta { get; }
    }

    /// <summary>The authored mission: map, encounters, economy and combat rules.</summary>
    public sealed class MissionDefinition
    {
        private readonly TerrainType[] terrain;
        private readonly Dictionary<EncounterKind, EncounterDefinition> encountersByKind;

        public MissionDefinition(string id, string name, string description, string heroId, string clanId, string clanName, string clanHeroName,
            int startingGold, int armySlots, int daysPerWeek, TerrainRules terrainRules, double ownedPriceMultiplier, int width, int height,
            WorldPosition start, TerrainType[] terrain, CombatPolicy combat, IEnumerable<EncounterDefinition> encounters, IEnumerable<string> recruitmentIds)
        {
            Id = id;
            Name = name;
            Description = description;
            HeroId = heroId;
            ClanId = clanId;
            ClanName = clanName;
            ClanHeroName = clanHeroName;
            StartingGold = startingGold;
            ArmySlots = armySlots;
            DaysPerWeek = daysPerWeek;
            TerrainRules = terrainRules;
            OwnedPriceMultiplier = ownedPriceMultiplier;
            Width = width;
            Height = height;
            Start = start;
            this.terrain = (TerrainType[])terrain.Clone();
            Combat = combat;
            Encounters = encounters.ToList().AsReadOnly();
            RecruitmentIds = recruitmentIds.ToList().AsReadOnly();
            encountersByKind = Encounters.ToDictionary(encounter => encounter.Kind);
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string HeroId { get; }
        public string ClanName { get; }
        public string ClanId { get; }
        public string ClanHeroName { get; }
        public int StartingGold { get; }
        public int ArmySlots { get; }
        public int DaysPerWeek { get; }
        public TerrainRules TerrainRules { get; }
        /// <summary>Recruitment price multiplier once the player owns the town.</summary>
        public double OwnedPriceMultiplier { get; }
        public int Width { get; }
        public int Height { get; }
        public WorldPosition Start { get; }
        public CombatPolicy Combat { get; }
        public IReadOnlyList<EncounterDefinition> Encounters { get; }
        public IReadOnlyList<string> RecruitmentIds { get; }

        public TerrainType TerrainAt(WorldPosition position)
        {
            return terrain[position.Y * Width + position.X];
        }

        /// <summary>The single encounter of a kind; the first mission has exactly one of each.</summary>
        public EncounterDefinition Encounter(EncounterKind kind)
        {
            return encountersByKind[kind];
        }

        public bool HasEncounter(string encounterId)
        {
            foreach (EncounterDefinition encounter in Encounters)
            {
                if (encounter.Id == encounterId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsRecruitable(string unitId)
        {
            foreach (string id in RecruitmentIds)
            {
                if (id == unitId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
