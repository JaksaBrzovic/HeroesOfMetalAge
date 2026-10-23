using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// One secondary skill: its name, description, icon and its value at Basic, Advanced and Expert. What the value means
    /// depends on the skill:
    /// Archery, Offense: extra ranged / melee damage as a fraction (0.1 = +10%). Armorer: less damage taken as a fraction.
    /// Diplomacy: chance that a hostile army willing to talk offers to join. Estates: gold per day. Intelligence: extra
    /// maximum mana as a fraction. Leadership, Luck: morale / luck points. Logistics: extra land movement as a fraction.
    /// Mysticism: extra mana per day. Pathfinding: share of rough terrain's extra cost that is cut. Resistance: chance that a
    /// hostile spell fails on a stack. Sorcery: extra spell damage as a fraction. Tactics: columns the stacks can be placed in
    /// before a battle. Wisdom: the highest spell level that can be learned.
    /// </summary>
    [Serializable]
    public sealed class SkillEntry
    {
        [SerializeField] private SecondarySkill skill;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [Tooltip("Shown on the hero screen and in the level-up dialog; empty uses the placeholder art.")]
        [SerializeField] private Sprite icon;
        [Tooltip("The skill's value at Basic, Advanced and Expert (see the class summary for what it means).")]
        [SerializeField] private float[] values = new float[SkillDefinition.LEVEL_COUNT];

        public SkillEntry()
        {
        }

        internal SkillEntry(SecondarySkill skill, string displayName, string description, params float[] values)
        {
            this.skill = skill;
            this.displayName = displayName;
            this.description = description;
            this.values = values;
        }

        public SecondarySkill Skill => skill;
        public string DisplayName => displayName;
        public Sprite Icon
        {
            get => icon;
            internal set => icon = value;
        }

        public IReadOnlyList<float> Values => values ?? (values = new float[0]);

        public SkillDefinition ToDefinition()
        {
            return new SkillDefinition(skill, displayName, description, Values.Select(value => Math.Round((double)value, 6)));
        }
    }

    /// <summary>
    /// The rules of hero growth and magic: the experience table, the secondary skills with their values per level, luck,
    /// mana and Wisdom, Pathfinding's baseline, and the prices of Diplomacy and surrender. Defaults follow the classic hero
    /// games; the experience thresholds are those games' table scaled to this prototype's missions (x 0.3).
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Hero Rules")]
    public sealed class HeroRulesScriptableObject : ScriptableObject
    {
        [Header("Experience")]
        [Tooltip("Total experience to reach level 2, 3, 4... in increasing order.")]
        [SerializeField] private int[] levelExperience = { 300, 600, 960, 1380, 1860, 2400, 3000, 3660, 4410, 5250, 6180, 7296 };
        [Tooltip("Beyond the table every level costs the previous step times this.")]
        [Min(1f)]
        [SerializeField] private float experienceGrowth = 1.2f;
        [Min(2)]
        [SerializeField] private int maximumLevel = 75;

        [Header("Secondary skills")]
        [Tooltip("How many secondary skills a hero can know.")]
        [Range(1, 15)]
        [SerializeField] private int maximumSkills = 8;
        [SerializeField] private List<SkillEntry> skills = DefaultSkills();

        [Header("Luck")]
        [Tooltip("Chance per point of luck that a strike deals double (good luck) or half (bad luck) damage.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float luckChance = 1f / 24f;

        [Header("Magic")]
        [Tooltip("Maximum mana per point of knowledge, before Intelligence.")]
        [Min(1)]
        [SerializeField] private int manaPerKnowledge = 10;
        [Tooltip("Mana regained every day, before Mysticism.")]
        [Min(0)]
        [SerializeField] private int manaRegeneration = 1;
        [Tooltip("The highest spell level a hero without Wisdom can learn.")]
        [Range(1, 5)]
        [SerializeField] private int spellLevelWithoutWisdom = 2;
        [Tooltip("Entering the player's own town restores the hero's mana.")]
        [SerializeField] private bool isManaRestoredInTown = true;

        [Header("Pathfinding")]
        [Tooltip("Pathfinding cuts only what a terrain costs above this (plain ground's cost).")]
        [Min(1)]
        [SerializeField] private int unpenalizedTerrainCost = 100;

        [Header("Diplomacy and surrender")]
        [Tooltip("A hostile army considers joining only when the hero's army is at least this many times stronger.")]
        [Min(0f)]
        [SerializeField] private float joinStrengthRatio = 2f;
        [Tooltip("An army that joins asks this share of its recruitment price.")]
        [Min(0f)]
        [SerializeField] private float joinCostMultiplier = 1f;
        [Tooltip("Surrendering to an enemy hero costs this share of the surviving army's recruitment price.")]
        [Min(0f)]
        [SerializeField] private float surrenderCostMultiplier = 0.5f;
        [Tooltip("Diplomacy's discount on surrendering at Basic, Advanced and Expert.")]
        [SerializeField] private float[] surrenderDiscounts = { 0.2f, 0.4f, 0.6f };

        public IReadOnlyList<int> LevelExperience => levelExperience ?? new int[0];
        public float ExperienceGrowth => experienceGrowth;
        public int MaximumLevel => maximumLevel;
        public int MaximumSkills => maximumSkills;
        public IReadOnlyList<SkillEntry> Skills => skills;
        public float LuckChance => luckChance;
        public int ManaPerKnowledge => manaPerKnowledge;
        public int ManaRegeneration => manaRegeneration;
        public int SpellLevelWithoutWisdom => spellLevelWithoutWisdom;
        public float JoinStrengthRatio => joinStrengthRatio;
        public float JoinCostMultiplier => joinCostMultiplier;
        public float SurrenderCostMultiplier => surrenderCostMultiplier;
        public IReadOnlyList<float> SurrenderDiscounts => surrenderDiscounts ?? new float[0];
        public int UnpenalizedTerrainCost => unpenalizedTerrainCost;

        public HeroPolicy ToPolicy()
        {
            ExperienceTable experience = new ExperienceTable(levelExperience, Math.Round((double)experienceGrowth, 6), maximumLevel);
            return new HeroPolicy(experience, maximumSkills, skills.Select(entry => entry.ToDefinition()), Math.Round((double)luckChance, 6),
                manaPerKnowledge, manaRegeneration, spellLevelWithoutWisdom, isManaRestoredInTown, unpenalizedTerrainCost,
                Math.Round((double)joinStrengthRatio, 6), Math.Round((double)joinCostMultiplier, 6), Math.Round((double)surrenderCostMultiplier, 6),
                SurrenderDiscounts.Select(value => Math.Round((double)value, 6)));
        }

        /// <summary>The skills of the classic hero games with their values at Basic, Advanced and Expert.</summary>
        internal static List<SkillEntry> DefaultSkills()
        {
            return new List<SkillEntry>
            {
                new SkillEntry(SecondarySkill.Archery, "Archery", "Ranged shots of your stacks deal more damage.", 0.1f, 0.25f, 0.5f),
                new SkillEntry(SecondarySkill.Armorer, "Armorer", "Your stacks take less damage from every strike.", 0.05f, 0.1f, 0.15f),
                new SkillEntry(SecondarySkill.Offense, "Offense", "Your stacks deal more damage in melee.", 0.1f, 0.2f, 0.3f),
                new SkillEntry(SecondarySkill.Diplomacy, "Diplomacy", "Weaker hostile armies may offer to join you for gold, and surrendering to an enemy hero costs less.",
                    0.25f, 0.5f, 1f),
                new SkillEntry(SecondarySkill.Estates, "Estates", "Your lands bring gold every day.", 125f, 250f, 500f),
                new SkillEntry(SecondarySkill.Intelligence, "Intelligence", "Your hero holds more mana.", 0.25f, 0.5f, 1f),
                new SkillEntry(SecondarySkill.Leadership, "Leadership", "Raises the morale of your army.", 1f, 2f, 3f),
                new SkillEntry(SecondarySkill.Logistics, "Logistics", "Your hero rides farther every day.", 0.1f, 0.2f, 0.3f),
                new SkillEntry(SecondarySkill.Luck, "Luck", "Raises the luck of your army.", 1f, 2f, 3f),
                new SkillEntry(SecondarySkill.Mysticism, "Mysticism", "Your hero regains more mana every day.", 2f, 3f, 4f),
                new SkillEntry(SecondarySkill.Pathfinding, "Pathfinding", "Rough terrain slows your hero less.", 0.25f, 0.5f, 0.75f),
                new SkillEntry(SecondarySkill.Resistance, "Resistance", "Hostile spells may fail against your stacks.", 0.05f, 0.1f, 0.2f),
                new SkillEntry(SecondarySkill.Sorcery, "Sorcery", "Your spells deal more damage.", 0.05f, 0.1f, 0.15f),
                new SkillEntry(SecondarySkill.Tactics, "Tactics", "Before a battle you place your stacks within the first columns of the field.", 2f, 3f, 5f),
                new SkillEntry(SecondarySkill.Wisdom, "Wisdom", "Your hero can learn spells of higher levels.", 3f, 4f, 5f)
            };
        }
    }
}
