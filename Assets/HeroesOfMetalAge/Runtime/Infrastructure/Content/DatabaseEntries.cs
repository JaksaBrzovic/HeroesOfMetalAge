using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using MLin.Audio;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    // Authored database rows. Setters are internal so validation tests can edit copies; designers edit the assets.

    [Serializable]
    public sealed class UnitEntry
    {
        [Tooltip("Stable ID used by saves; do not rename without migrating existing saves.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite portrait;
        [Tooltip("Right-facing walk cycle shown on the map and battlefield while the stack moves; empty for still art.")]
        [SerializeField] private Sprite[] walkFrames = new Sprite[0];
        [Min(1f)]
        [SerializeField] private float walkFramesPerSecond = 8f;
        [Tooltip("Placeholder art used when no portrait is assigned: axe, bow or rider.")]
        [SerializeField] private string placeholderIcon = "axe";
        [Min(1)]
        [SerializeField] private int health = 1;
        [Min(1)]
        [SerializeField] private int minimumDamage = 1;
        [Min(1)]
        [SerializeField] private int maximumDamage = 1;
        [Min(0)]
        [SerializeField] private int attack;
        [Min(0)]
        [SerializeField] private int defense;
        [Min(1)]
        [SerializeField] private int speed = 1;
        [Min(1)]
        [SerializeField] private int recruitmentCost = 1;
        [Min(0)]
        [SerializeField] private int weeklyGrowth;
        [SerializeField] private bool ranged;
        [Tooltip("Played when the unit joins, is met or acts in battle; route it to the Voice channel. Leave empty for a silent unit.")]
        [SerializeField] private SoundDefinition voice;

        public string Id => id;
        public Sprite Portrait => portrait;
        public IReadOnlyList<Sprite> WalkFrames => walkFrames;
        public float WalkFramesPerSecond => walkFramesPerSecond;

        public SoundDefinition Voice
        {
            get => voice;
            internal set => voice = value;
        }

        public int Health
        {
            get => health;
            internal set => health = value;
        }

        public int MinimumDamage
        {
            get => minimumDamage;
            internal set => minimumDamage = value;
        }

        public int MaximumDamage
        {
            get => maximumDamage;
            internal set => maximumDamage = value;
        }

        public int Attack => attack;
        public int Defense => defense;
        public int Speed => speed;
        public int RecruitmentCost => recruitmentCost;
        public int WeeklyGrowth => weeklyGrowth;

        public UnitDefinition ToDefinition()
        {
            return new UnitDefinition(id, displayName, description, health, minimumDamage, maximumDamage, attack, defense, speed, recruitmentCost,
                weeklyGrowth, ranged, placeholderIcon);
        }
    }

    [Serializable]
    public sealed class ArtefactEntry
    {
        [Tooltip("Stable ID used by saves.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private EquipmentSlot slot;
        [Tooltip("How rarely the artefact drops as a battle reward.")]
        [SerializeField] private ArtefactRarity rarity;
        [Min(0)]
        [SerializeField] private int attackBonus;
        [Min(0)]
        [SerializeField] private int defenseBonus;
        [Min(0)]
        [SerializeField] private int speedBonus;

        public string Id => id;
        public Sprite Icon => icon;
        public EquipmentSlot Slot => slot;
        public ArtefactRarity Rarity => rarity;

        public int AttackBonus
        {
            get => attackBonus;
            internal set => attackBonus = value;
        }

        public int DefenseBonus
        {
            get => defenseBonus;
            internal set => defenseBonus = value;
        }

        public int SpeedBonus => speedBonus;

        public ArtefactDefinition ToDefinition()
        {
            return new ArtefactDefinition(id, displayName, description, slot, rarity, attackBonus, defenseBonus, speedBonus);
        }
    }

    [Serializable]
    public sealed class ArmyEntry
    {
        [SerializeField] private string unitId;
        [Min(1)]
        [SerializeField] private int count = 1;

        public string UnitId => unitId;
        public int Count => count;

        public Troop ToTroop()
        {
            return new Troop(unitId, count);
        }
    }

    [Serializable]
    public sealed class HeroEntry
    {
        [Tooltip("Stable ID used by saves.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string className;
        [SerializeField] private string faction;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private Sprite portrait;
        [Tooltip("Right-facing ride cycle shown on the adventure map while the hero travels.")]
        [SerializeField] private Sprite[] mapFrames = new Sprite[0];
        [Min(1f)]
        [SerializeField] private float mapFramesPerSecond = 11f;
        [Min(0)]
        [SerializeField] private int baseAttack;
        [Min(0)]
        [SerializeField] private int baseDefense;
        [Min(1)]
        [SerializeField] private int dailyMovement = 1600;
        [Min(1)]
        [SerializeField] private int experiencePerLevel = 300;
        [Min(0)]
        [SerializeField] private int attackPerLevel = 1;
        [Tooltip("Defense grows by one every this many levels.")]
        [Min(1)]
        [SerializeField] private int defenseLevelInterval = 2;
        [SerializeField] private List<ArmyEntry> startingArmy = new List<ArmyEntry>();

        public string Id => id;

        public string DisplayName
        {
            get => displayName;
            internal set => displayName = value;
        }

        public string Description
        {
            get => description;
            internal set => description = value;
        }

        public Sprite Portrait
        {
            get => portrait;
            internal set => portrait = value;
        }

        public IReadOnlyList<Sprite> MapFrames => mapFrames;
        public float MapFramesPerSecond => mapFramesPerSecond;
        public int BaseAttack => baseAttack;
        public int BaseDefense => baseDefense;
        public int DailyMovement => dailyMovement;
        public int ExperiencePerLevel => experiencePerLevel;
        public int AttackPerLevel => attackPerLevel;
        public int DefenseLevelInterval => defenseLevelInterval;
        public IReadOnlyList<ArmyEntry> StartingArmy => startingArmy;

        public HeroDefinition ToDefinition()
        {
            return new HeroDefinition(id, displayName, description, className, faction, baseAttack, baseDefense, dailyMovement, experiencePerLevel,
                attackPerLevel, defenseLevelInterval, startingArmy.Select(entry => entry.ToTroop()));
        }
    }

    [Serializable]
    public sealed class EncounterEntry
    {
        [Tooltip("Stable ID used by saves.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private EncounterKind kind;
        [SerializeField] private Vector2Int position;
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private List<ArmyEntry> army = new List<ArmyEntry>();
        [Min(0)]
        [SerializeField] private int rewardExperience;
        [Tooltip("Typical gold for winning; the mission's reward settings vary it and roll the artefacts.")]
        [Min(0)]
        [SerializeField] private int rewardGold;
        [Tooltip("Battlefield image for this encounter; empty uses the mission's.")]
        [SerializeField] private Sprite battleBackground;
        [Tooltip("Dialogue conversation played when the hero reaches this site: before the friendly or hostile dialog opens, or "
            + "in the town from the garrison hero while the clan still holds it. Empty for none.")]
        [SerializeField] private string arrivalConversationId;

        public string Id => id;
        public EncounterKind Kind => kind;
        public Vector2Int Position => position;

        public List<ArmyEntry> Army
        {
            get => army;
            internal set => army = value;
        }

        public int RewardExperience => rewardExperience;
        public int RewardGold => rewardGold;
        public Sprite BattleBackground => battleBackground;

        public string ArrivalConversationId
        {
            get => arrivalConversationId;
            internal set => arrivalConversationId = value;
        }

        public EncounterDefinition ToDefinition()
        {
            return new EncounterDefinition(id, kind, new WorldPosition(position.x, position.y), displayName, description,
                army.Select(entry => entry.ToTroop()), rewardExperience, rewardGold);
        }
    }
}
