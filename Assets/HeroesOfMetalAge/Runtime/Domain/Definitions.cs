using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// The kind of an artefact, which decides where the hero wears it. Values are stored in the artefact database, so
    /// existing values never change; new kinds go at the end. <see cref="Charm"/> is worn as the amulet.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon,
        Charm,
        Head,
        Shield,
        Gloves,
        Torso,
        /// <summary>Worn on either hand: the left ring place first, then the right.</summary>
        Ring,
        Necklace,
        Sash,
        Boots
    }

    /// <summary>The eleven places on the hero where artefacts are worn, in the order the hero screen lists them.</summary>
    public enum EquipmentPlace
    {
        Head,
        LeftArm,
        RightArm,
        Gloves,
        Torso,
        LeftRing,
        RightRing,
        Amulet,
        Necklace,
        Sash,
        Boots
    }

    public static class Equipment
    {
        public const int PLACE_COUNT = 11;

        /// <summary>The places an artefact of the slot can be worn in, preferred first.</summary>
        public static EquipmentPlace[] Places(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Weapon:
                    return new[] { EquipmentPlace.RightArm };
                case EquipmentSlot.Charm:
                    return new[] { EquipmentPlace.Amulet };
                case EquipmentSlot.Head:
                    return new[] { EquipmentPlace.Head };
                case EquipmentSlot.Shield:
                    return new[] { EquipmentPlace.LeftArm };
                case EquipmentSlot.Gloves:
                    return new[] { EquipmentPlace.Gloves };
                case EquipmentSlot.Torso:
                    return new[] { EquipmentPlace.Torso };
                case EquipmentSlot.Ring:
                    return new[] { EquipmentPlace.LeftRing, EquipmentPlace.RightRing };
                case EquipmentSlot.Necklace:
                    return new[] { EquipmentPlace.Necklace };
                case EquipmentSlot.Sash:
                    return new[] { EquipmentPlace.Sash };
                default:
                    return new[] { EquipmentPlace.Boots };
            }
        }

        public static bool Fits(EquipmentSlot slot, EquipmentPlace place)
        {
            return Array.IndexOf(Places(slot), place) >= 0;
        }
    }

    /// <summary>
    /// Terrain of a map cell. Values are stored in painted logic tiles, so existing values never change; new types go at the
    /// end. Which types are passable and what they cost comes from <see cref="TerrainRules"/>.
    /// </summary>
    public enum TerrainType
    {
        Grass,
        Forest,
        Road,
        Mountain,
        Water,
        /// <summary>Not painted on the map: impassable.</summary>
        Blocked,
        Dirt,
        Rough,
        Sand,
        Swamp,
        Lava,
        /// <summary>Rocks, ruins and other scenery the hero cannot ride through.</summary>
        Obstacle
    }

    /// <summary>How rare an artefact is when battle rewards are rolled.</summary>
    public enum ArtefactRarity
    {
        Common,
        Rare,
        Legendary,
        /// <summary>Between rare and legendary; stored after them because the values are saved in the artefact database.</summary>
        Epic
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
        public ArtefactDefinition(string id, string name, string description, EquipmentSlot slot, ArtefactRarity rarity, int attackBonus,
            int defenseBonus, int speedBonus, int moraleBonus = 0)
        {
            Id = id;
            Name = name;
            Description = description;
            Slot = slot;
            Rarity = rarity;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            SpeedBonus = speedBonus;
            MoraleBonus = moraleBonus;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public EquipmentSlot Slot { get; }
        public ArtefactRarity Rarity { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int SpeedBonus { get; }
        public int MoraleBonus { get; }
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
            int rewardExperience, int rewardGold, string rewardArtefactId = null)
        {
            Id = id;
            Kind = kind;
            Position = position;
            Name = name;
            Description = description;
            Army = army.ToList().AsReadOnly();
            RewardExperience = rewardExperience;
            RewardGold = rewardGold;
            RewardArtefactId = string.IsNullOrEmpty(rewardArtefactId) ? null : rewardArtefactId;
        }

        public string Id { get; }
        public EncounterKind Kind { get; }
        public WorldPosition Position { get; }
        public string Name { get; }
        public string Description { get; }
        public IReadOnlyList<Troop> Army { get; }
        public int RewardExperience { get; }
        /// <summary>Typical gold for winning; the rolled amount varies around it (see <see cref="RewardPolicy"/>).</summary>
        public int RewardGold { get; }
        /// <summary>The treasure the army guards: always won with it instead of a rolled artefact. Null for none.</summary>
        public string RewardArtefactId { get; }
        public bool IsHostile => Kind == EncounterKind.Sentinel || Kind == EncounterKind.Boss;
    }
}
