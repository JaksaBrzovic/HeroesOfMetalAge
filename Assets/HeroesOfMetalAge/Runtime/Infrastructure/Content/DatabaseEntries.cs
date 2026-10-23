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
        [Tooltip("The figure standing on the map and the battlefield (usually the walk cycle's first frame).")]
        [SerializeField] private Sprite portrait;
        [Tooltip("The picture in slots, recruitment rows and the battle panel; empty uses the figure.")]
        [SerializeField] private Sprite cardPortrait;
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
        public Sprite CardPortrait => cardPortrait;
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
        [Tooltip("Morale while worn, -3..3: good morale lets stacks act again, bad morale freezes them.")]
        [Range(-3, 3)]
        [SerializeField] private int moraleBonus;

        public string Id => id;
        public string DisplayName => displayName;
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
        public int MoraleBonus => moraleBonus;

        public ArtefactDefinition ToDefinition()
        {
            return new ArtefactDefinition(id, displayName, description, slot, rarity, attackBonus, defenseBonus, speedBonus, moraleBonus);
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
        [Tooltip("The figure on the adventure map while standing (usually the ride cycle's first frame).")]
        [SerializeField] private Sprite portrait;
        [Tooltip("The face in the adventure screen's hero panel; empty uses the figure.")]
        [SerializeField] private Sprite panelPortrait;
        [Tooltip("The large picture on the hero screen; empty uses the panel portrait.")]
        [SerializeField] private Sprite screenPortrait;
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

        public Sprite PanelPortrait => panelPortrait;
        public Sprite ScreenPortrait => screenPortrait;
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
        [Tooltip("Artefact this army guards: always won with it instead of a rolled one. Empty for none.")]
        [SerializeField] private string rewardArtefactId;
        [Tooltip("Battlefield image for this encounter; empty uses the mission's.")]
        [SerializeField] private Sprite battleBackground;
        [Tooltip("Figure shown on the map instead of the army's first unit, e.g. a creature that is not in the unit database's art.")]
        [SerializeField] private Sprite mapSprite;
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
        public string RewardArtefactId => rewardArtefactId;
        public Sprite BattleBackground => battleBackground;
        public Sprite MapSprite => mapSprite;

        public string ArrivalConversationId
        {
            get => arrivalConversationId;
            internal set => arrivalConversationId = value;
        }

        public EncounterDefinition ToDefinition()
        {
            return new EncounterDefinition(id, kind, new WorldPosition(position.x, position.y), displayName, description,
                army.Select(entry => entry.ToTroop()), rewardExperience, rewardGold, rewardArtefactId);
        }
    }

    [Serializable]
    public sealed class MapObjectEntry
    {
        [Tooltip("Stable ID used by saves and by goals.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private MapObjectKind kind;
        [Tooltip("Cell from the top-left corner of the map.")]
        [SerializeField] private Vector2Int position;
        [TextArea]
        [SerializeField] private string description;
        [Tooltip("Shown on the map; empty uses the placeholder token of its kind from the UI theme.")]
        [SerializeField] private Sprite sprite;
        [Tooltip("Height on the map in cells (1 is one cell high).")]
        [Range(0.5f, 4f)]
        [SerializeField] private float mapHeight = 1.2f;
        [Header("What it gives")]
        [Tooltip("Gold pile: its gold. Chest: the gold choice. Mill: gold per week.")]
        [Min(0)]
        [SerializeField] private int gold;
        [Tooltip("Chest: the experience choice.")]
        [Min(0)]
        [SerializeField] private int experience;
        [Tooltip("Artefact pickup: the artefact. Prison: the captive's gift.")]
        [SerializeField] private string artefactId;
        [Min(0)]
        [SerializeField] private int attackBonus;
        [Min(0)]
        [SerializeField] private int defenseBonus;
        [Range(0, 3)]
        [SerializeField] private int moraleBonus;
        [Tooltip("Dwelling: the creature for sale.")]
        [SerializeField] private string unitId;
        [Tooltip("Dwelling: creatures added each week (and its stock at the start).")]
        [Min(0)]
        [SerializeField] private int weeklyGrowth;
        [Tooltip("Quest giver: the key handed over. Border gate: the key that opens it.")]
        [SerializeField] private string keyId;
        [Tooltip("Dialogue conversation played when the hero reaches the object. Empty for none.")]
        [SerializeField] private string arrivalConversationId;

        public string Id => id;
        public string DisplayName => displayName;
        public MapObjectKind Kind => kind;
        public Vector2Int Position => position;
        public Sprite Sprite => sprite;
        public float MapHeight => mapHeight;
        public int Gold => gold;
        public int Experience => experience;
        public string ArtefactId => artefactId;
        public int AttackBonus => attackBonus;
        public int DefenseBonus => defenseBonus;
        public int MoraleBonus => moraleBonus;
        public string UnitId => unitId;
        public int WeeklyGrowth => weeklyGrowth;
        public string KeyId => keyId;
        public string ArrivalConversationId => arrivalConversationId;

        public MapObjectDefinition ToDefinition()
        {
            return new MapObjectDefinition(id, kind, new WorldPosition(position.x, position.y), displayName, description, gold, experience, artefactId,
                attackBonus, defenseBonus, moraleBonus, unitId, weeklyGrowth, keyId);
        }
    }

    [Serializable]
    public sealed class ObjectiveEntry
    {
        [Tooltip("Stable ID.")]
        [SerializeField] private string id;
        [Tooltip("The goal as the mission panel lists it.")]
        [SerializeField] private string text;
        [SerializeField] private ObjectiveKind kind;
        [Tooltip("Encounter, map object or artefact ID, depending on the kind.")]
        [SerializeField] private string targetId;
        [Tooltip("Reach level: the level.")]
        [Min(0)]
        [SerializeField] private int value;
        [Tooltip("The exit portal opens only once every required goal is reached.")]
        [SerializeField] private bool isRequired;

        public string Id => id;
        public string Text => text;
        public ObjectiveKind Kind => kind;
        public string TargetId => targetId;
        public int Value => value;
        public bool IsRequired => isRequired;

        public ObjectiveDefinition ToDefinition()
        {
            return new ObjectiveDefinition(id, text, kind, targetId, value, isRequired);
        }
    }
}
