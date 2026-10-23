using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>A secondary skill's weight in a class's level-up offers.</summary>
    [Serializable]
    public sealed class SkillWeightEntry
    {
        [SerializeField] private SecondarySkill skill;
        [Tooltip("Relative chance among the class's skills; 0 means the class never gets the skill.")]
        [Min(0)]
        [SerializeField] private int weight;

        public SkillWeightEntry()
        {
        }

        internal SkillWeightEntry(SecondarySkill skill, int weight)
        {
            this.skill = skill;
            this.weight = weight;
        }

        public SecondarySkill Skill => skill;
        public int Weight => weight;
    }

    /// <summary>A hero class: how its heroes grow on level-ups.</summary>
    [Serializable]
    public sealed class HeroClassEntry
    {
        [Tooltip("Stable ID heroes refer to.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [Tooltip("Chance weights of the primary stat a level-up raises below the high level: attack, defense, spell power, knowledge.")]
        [SerializeField] private int[] lowLevelPrimaryWeights = { 35, 45, 10, 10 };
        [Tooltip("The same from the high level on.")]
        [SerializeField] private int[] highLevelPrimaryWeights = { 30, 30, 20, 20 };
        [Tooltip("The level from which the high-level weights apply.")]
        [Min(2)]
        [SerializeField] private int highLevelFrom = 10;
        [Tooltip("Wisdom is offered at the latest on every this many level-ups, while it can still be learned; 0 never forces it.")]
        [Min(0)]
        [SerializeField] private int wisdomInterval = 6;
        [Tooltip("The secondary skills the class can be offered and their weights; a skill missing here or at 0 is never offered.")]
        [SerializeField] private List<SkillWeightEntry> skillWeights = new List<SkillWeightEntry>();

        public HeroClassEntry()
        {
        }

        internal HeroClassEntry(string id, string displayName, string description, IEnumerable<SkillWeightEntry> skillWeights)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.skillWeights = skillWeights.ToList();
        }

        public string Id => id;
        public string DisplayName => displayName;
        public IReadOnlyList<int> LowLevelPrimaryWeights => lowLevelPrimaryWeights ?? new int[0];
        public IReadOnlyList<int> HighLevelPrimaryWeights => highLevelPrimaryWeights ?? new int[0];
        public int HighLevelFrom => highLevelFrom;
        public int WisdomInterval => wisdomInterval;
        public IReadOnlyList<SkillWeightEntry> SkillWeights => skillWeights ?? (skillWeights = new List<SkillWeightEntry>());

        public HeroClassDefinition ToDefinition()
        {
            Dictionary<SecondarySkill, int> weights = new Dictionary<SecondarySkill, int>();
            foreach (SkillWeightEntry entry in SkillWeights)
            {
                weights[entry.Skill] = entry.Weight;
            }

            return new HeroClassDefinition(id, displayName, description, LowLevelPrimaryWeights, HighLevelPrimaryWeights, highLevelFrom, weights,
                wisdomInterval);
        }
    }

    /// <summary>Authored hero classes; validated and projected into the gameplay catalog at startup.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Hero Class Database")]
    public sealed class HeroClassDatabaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<HeroClassEntry> classes = new List<HeroClassEntry>();

        public IReadOnlyList<HeroClassEntry> Classes => classes;

        internal void SetClasses(IEnumerable<HeroClassEntry> entries)
        {
            classes = entries.ToList();
        }
    }
}
