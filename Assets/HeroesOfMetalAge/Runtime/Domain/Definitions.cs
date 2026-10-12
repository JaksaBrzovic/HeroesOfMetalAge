using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    public enum EquipmentSlot
    {
        Weapon,
        Charm
    }

    public enum TerrainType
    {
        Grass,
        Forest,
        Road,
        Mountain,
        Water,
        /// <summary>Not painted on the map: impassable.</summary>
        Blocked
    }

    public enum EncounterKind
    {
        Friendly,
        Sentinel,
        Town,
        Boss
    }

    /// <summary>Validated, immutable creature definition projected from the authored unit database.</summary>
    public sealed class UnitDefinition
    {
        public UnitDefinition(string id, string name, string description, int health, int minimumDamage, int maximumDamage, int attack, int defense,
            int speed, int cost, int weeklyGrowth, bool isRanged, string iconKey)
        {
            Id = id;
            Name = name;
            Description = description;
            Health = health;
            MinimumDamage = minimumDamage;
            MaximumDamage = maximumDamage;
            Attack = attack;
            Defense = defense;
            Speed = speed;
            Cost = cost;
            WeeklyGrowth = weeklyGrowth;
            IsRanged = isRanged;
            IconKey = iconKey;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>Placeholder art key used when no portrait is authored.</summary>
        public string IconKey { get; }
        public int Health { get; }
        public int MinimumDamage { get; }
        public int MaximumDamage { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int Speed { get; }
        public int Cost { get; }
        public int WeeklyGrowth { get; }
        public bool IsRanged { get; }
    }

    public sealed class ArtefactDefinition
    {
        public ArtefactDefinition(string id, string name, string description, EquipmentSlot slot, int attackBonus, int defenseBonus, int speedBonus)
        {
            Id = id;
            Name = name;
            Description = description;
            Slot = slot;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            SpeedBonus = speedBonus;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EquipmentSlot Slot { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int SpeedBonus { get; }
    }

    public sealed class HeroDefinition
    {
        public HeroDefinition(string id, string name, string description, string className, string faction, int baseAttack, int baseDefense,
            int dailyMovement, int experiencePerLevel, int attackPerLevel, int defenseLevelInterval, IEnumerable<Troop> startingArmy)
        {
            Id = id;
            Name = name;
            Description = description;
            ClassName = className;
            Faction = faction;
            BaseAttack = baseAttack;
            BaseDefense = baseDefense;
            DailyMovement = dailyMovement;
            ExperiencePerLevel = experiencePerLevel;
            AttackPerLevel = attackPerLevel;
            DefenseLevelInterval = defenseLevelInterval;
            StartingArmy = startingArmy.ToList().AsReadOnly();
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public string ClassName { get; }
        public string Faction { get; }
        public int BaseAttack { get; }
        public int BaseDefense { get; }
        public int DailyMovement { get; }
        public int ExperiencePerLevel { get; }
        public int AttackPerLevel { get; }
        /// <summary>Defense grows by one every this many levels.</summary>
        public int DefenseLevelInterval { get; }
        public IReadOnlyList<Troop> StartingArmy { get; }
    }

    public sealed class EncounterDefinition
    {
        public EncounterDefinition(string id, EncounterKind kind, WorldPosition position, string name, string description, IEnumerable<Troop> army,
            int rewardExperience, int rewardGold, string rewardArtefactId)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Name = name;
            Description = description;
            Army = army.ToList().AsReadOnly();
            RewardExperience = rewardExperience;
            RewardGold = rewardGold;
            RewardArtefactId = rewardArtefactId;
        }

        public string Id { get; }
        public EncounterKind Kind { get; }
        public WorldPosition Position { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<Troop> Army { get; }
        public int RewardExperience { get; }
        public int RewardGold { get; }
        /// <summary>Artefact granted on victory; null or empty when there is none.</summary>
        public string RewardArtefactId { get; }
    }
}
