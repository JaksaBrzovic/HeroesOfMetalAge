using System.Collections.Generic;
using MLin.Audio;
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
        [Tooltip("Recruitment price multiplier once the player owns the town.")]
        [Range(0.01f, 1f)]
        [SerializeField] private float ownedRecruitmentMultiplier = 0.55f;
        [Tooltip("Adventure map prefab: map image, cell size and the terrain logic layer.")]
        [SerializeField] private AdventureMapAuthoring map;
        [Tooltip("Passable terrain, movement costs and the diagonal step cost.")]
        [SerializeField] private TerrainDatabaseScriptableObject terrain;
        [Tooltip("Cell from the top-left corner of the map; hover the map in the Scene view to read a cell's coordinates.")]
        [SerializeField] private Vector2Int startingPosition = new Vector2Int(2, 5);
        [SerializeField] private List<string> recruitmentUnitIds = new List<string>();
        [Tooltip("Encounter positions are cells from the top-left corner of the map.")]
        [SerializeField] private List<EncounterEntry> encounters = new List<EncounterEntry>();

        [Header("Music")]
        [Tooltip("Plays on the adventure map. Empty leaves the map to the ambient and gallop layers.")]
        [SerializeField] private SoundDefinition mapMusic;
        [Tooltip("Plays on the town screen. Empty keeps the map music.")]
        [SerializeField] private SoundDefinition townMusic;

        [Header("Combat")]
        [Tooltip("Battlefield image; the hex board is laid over it. Empty uses the Battlefield prefab's image.")]
        [SerializeField] private Sprite battleBackground;
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
        [Tooltip("Chance that a strike misses and deals no damage.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float missChance = 0.15f;

        [Header("Battle rewards")]
        [Tooltip("Rolled gold is an encounter's gold times a factor between 1 - this and 1 + this.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float goldVariation = 0.25f;
        [Tooltip("Chance that a victory drops an artefact (at most one).")]
        [Range(0f, 1f)]
        [SerializeField] private float artefactChance = 0.5f;
        [Tooltip("Chance that a dropped artefact is rare; what remains after rare and legendary is common.")]
        [Range(0f, 1f)]
        [SerializeField] private float rareChance = 0.3f;
        [Range(0f, 1f)]
        [SerializeField] private float legendaryChance = 0.1f;
        [Tooltip("Every this many victories bring at least one legendary artefact; 0 turns the guarantee off.")]
        [Min(0)]
        [SerializeField] private int legendaryGuaranteeVictories = 4;

        [Header("Presentation pacing")]
        [Min(0.01f)]
        [SerializeField] private float travelStepSeconds = 0.15f;
        [Min(0.01f)]
        [SerializeField] private float enemyTurnSeconds = 0.7f;
        [Tooltip("Pause after every battle action (player or enemy) before the next one can be made, so actions cannot be clicked through.")]
        [Min(0f)]
        [SerializeField] private float battleActionSeconds = 1.5f;
        [Tooltip("Seconds the battlefield stays on screen after the last blow before the victory or defeat screen opens.")]
        [Min(0f)]
        [SerializeField] private float battleResultSeconds = 2f;

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
        public float OwnedRecruitmentMultiplier => ownedRecruitmentMultiplier;
        public AdventureMapAuthoring Map
        {
            get => map;
            internal set => map = value;
        }

        public TerrainDatabaseScriptableObject Terrain
        {
            get => terrain;
            internal set => terrain = value;
        }

        public Vector2Int StartingPosition => startingPosition;
        public IReadOnlyList<string> RecruitmentUnitIds => recruitmentUnitIds;
        public Sprite BattleBackground => battleBackground;
        public SoundDefinition MapMusic => mapMusic;
        public SoundDefinition TownMusic => townMusic;
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
        public float MissChance => missChance;
        public float GoldVariation => goldVariation;
        public float ArtefactChance => artefactChance;
        public float RareChance => rareChance;
        public float LegendaryChance => legendaryChance;
        public int LegendaryGuaranteeVictories => legendaryGuaranteeVictories;
        public float TravelStepSeconds => travelStepSeconds;
        public float EnemyTurnSeconds => enemyTurnSeconds;
        public float BattleActionSeconds => battleActionSeconds;
        public float BattleResultSeconds => battleResultSeconds;
    }
}
