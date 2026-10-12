using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>Authored mission: map, encounters, economy, combat tuning and presentation pacing.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Mission Settings")]
    public sealed class MissionSettingsScriptableObject : ScriptableObject
    {
        [Tooltip("Stable mission ID stored in saves.")]
        [SerializeField] private string id = "ashen-pass";
        [SerializeField] private string displayName = "THE ASHEN PASS";
        [SerializeField] private string heroId = "kael";
        [TextArea]
        [SerializeField] private string description;
        [SerializeField] private string clanId = "ashen-clan";
        [SerializeField] private string clanName = "Ashen Clan";
        [SerializeField] private string clanHeroName = "Lord Varg";
        [SerializeField] private Sprite townPortrait;
        [Min(0)]
        [SerializeField] private int startingGold = 1600;
        [Range(1, 7)]
        [SerializeField] private int armySlots = 7;
        [Min(1)]
        [SerializeField] private int daysPerWeek = 7;
        [Min(1)]
        [SerializeField] private int forestMovementCost = 2;
        [Min(1)]
        [SerializeField] private int groundMovementCost = 1;
        [Tooltip("Recruitment price multiplier once the player owns the town.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float ownedRecruitmentMultiplier = 0.55f;
        [SerializeField] private Vector2Int startingPosition = new Vector2Int(2, 5);
        [Range(4, 18)]
        [SerializeField] private int mapWidth = 18;
        [Range(4, 11)]
        [SerializeField] private int mapHeight = 11;
        [SerializeField] private List<TerrainRow> terrainRows = new List<TerrainRow>();
        [SerializeField] private List<string> recruitmentUnitIds = new List<string>();
        [SerializeField] private List<EncounterEntry> encounters = new List<EncounterEntry>();

        [Header("Combat rules")]
        [Range(6, 10)]
        [SerializeField] private int combatWidth = 10;
        [Range(7, 7)]
        [SerializeField] private int combatHeight = 7;
        [Min(0)]
        [SerializeField] private int enemyAttack = 1;
        [Min(0)]
        [SerializeField] private int enemyDefense = 1;
        [Range(0.01f, 1f)]
        [SerializeField] private float defendDamageMultiplier = 0.7f;
        [Range(0.01f, 1f)]
        [SerializeField] private float rangedMeleeDamageMultiplier = 0.5f;
        [Range(0f, 0.1f)]
        [SerializeField] private float attackDeltaMultiplier = 0.05f;
        [Range(0f, 0.1f)]
        [SerializeField] private float defenseDeltaMultiplier = 0.025f;
        [Min(0)]
        [SerializeField] private int maximumAttackDelta = 30;
        [Min(0)]
        [SerializeField] private int maximumDefenseDelta = 28;

        [Header("Presentation pacing")]
        [Min(0.01f)]
        [SerializeField] private float travelStepSeconds = 0.15f;
        [Min(0.01f)]
        [SerializeField] private float enemyTurnSeconds = 0.7f;

        public string Id => id;
        public string DisplayName => displayName;
        public string HeroId => heroId;
        public string Description => description;
        public string ClanId => clanId;
        public string ClanName => clanName;
        public string ClanHeroName => clanHeroName;
        public Sprite TownPortrait => townPortrait;
        public int StartingGold => startingGold;

        public int ArmySlots
        {
            get => armySlots;
            internal set => armySlots = value;
        }

        public int DaysPerWeek => daysPerWeek;
        public int ForestMovementCost => forestMovementCost;
        public int GroundMovementCost => groundMovementCost;
        public float OwnedRecruitmentMultiplier => ownedRecruitmentMultiplier;
        public Vector2Int StartingPosition => startingPosition;
        public int MapWidth => mapWidth;
        public int MapHeight => mapHeight;
        public IReadOnlyList<TerrainRow> TerrainRows => terrainRows;
        public IReadOnlyList<string> RecruitmentUnitIds => recruitmentUnitIds;
        public IReadOnlyList<EncounterEntry> Encounters => encounters;
        public int CombatWidth => combatWidth;
        public int CombatHeight => combatHeight;
        public int EnemyAttack => enemyAttack;
        public int EnemyDefense => enemyDefense;
        public float DefendDamageMultiplier => defendDamageMultiplier;
        public float RangedMeleeDamageMultiplier => rangedMeleeDamageMultiplier;
        public float AttackDeltaMultiplier => attackDeltaMultiplier;
        public float DefenseDeltaMultiplier => defenseDeltaMultiplier;
        public int MaximumAttackDelta => maximumAttackDelta;
        public int MaximumDefenseDelta => maximumDefenseDelta;
        public float TravelStepSeconds => travelStepSeconds;
        public float EnemyTurnSeconds => enemyTurnSeconds;
    }
}
