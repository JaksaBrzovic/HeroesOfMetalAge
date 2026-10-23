using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>The hero's four primary stats. Values are stored in hero class assets, so existing values never change.</summary>
    public enum PrimaryStat
    {
        Attack,
        Defense,
        SpellPower,
        Knowledge
    }

    /// <summary>
    /// Secondary skills a hero learns on level-up. Values are stored in the hero rules, class and hero assets and saves keep
    /// the name, so existing values never change; new skills go at the end.
    /// </summary>
    public enum SecondarySkill
    {
        /// <summary>More damage from ranged shots.</summary>
        Archery,
        /// <summary>Less damage taken by the hero's stacks.</summary>
        Armorer,
        /// <summary>More melee damage.</summary>
        Offense,
        /// <summary>Hostile armies may offer to join; surrendering costs less.</summary>
        Diplomacy,
        /// <summary>Gold every day.</summary>
        Estates,
        /// <summary>More maximum mana.</summary>
        Intelligence,
        /// <summary>Higher morale.</summary>
        Leadership,
        /// <summary>More movement on land.</summary>
        Logistics,
        /// <summary>Higher luck.</summary>
        Luck,
        /// <summary>More mana regained every day.</summary>
        Mysticism,
        /// <summary>Rough terrain costs less movement.</summary>
        Pathfinding,
        /// <summary>Hostile spells may fail against the hero's stacks.</summary>
        Resistance,
        /// <summary>More spell damage.</summary>
        Sorcery,
        /// <summary>The stacks can be placed before a battle begins.</summary>
        Tactics,
        /// <summary>Spells of higher levels can be learned.</summary>
        Wisdom
    }

    /// <summary>Mastery of a secondary skill; <see cref="None"/> means the hero does not know it.</summary>
    public enum SkillLevel
    {
        None,
        Basic,
        Advanced,
        Expert
    }

    /// <summary>A secondary skill the hero knows (or is offered) at a level. Immutable.</summary>
    public sealed class HeroSkill
    {
        public HeroSkill(SecondarySkill skill, SkillLevel level)
        {
            Skill = skill;
            Level = level;
        }

        public SecondarySkill Skill { get; }
        public SkillLevel Level { get; }
    }

    /// <summary>
    /// What a secondary skill does at each level. The meaning of <see cref="Values"/> depends on the skill (a fraction, an
    /// amount of gold, a number of columns...), see the hero rules asset.
    /// </summary>
    public sealed class SkillDefinition
    {
        public const int LEVEL_COUNT = 3;

        public SkillDefinition(SecondarySkill skill, string name, string description, IEnumerable<double> values)
        {
            Skill = skill;
            Name = name;
            Description = description;
            Values = values.ToList().AsReadOnly();
            if (Values.Count != LEVEL_COUNT)
            {
                throw new ArgumentException("A skill needs a value for Basic, Advanced and Expert: " + skill);
            }
        }

        public SecondarySkill Skill { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>Basic, Advanced and Expert values.</summary>
        public IReadOnlyList<double> Values { get; }

        /// <summary>The skill's value at a level; 0 when the hero does not know it.</summary>
        public double Value(SkillLevel level)
        {
            return level == SkillLevel.None ? 0d : Values[(int)level - 1];
        }
    }

    /// <summary>
    /// A hero class: how its heroes grow. On every level-up one primary stat rises by one, picked by the class's weights (one
    /// set below <see cref="HighLevelFrom"/>, another from it on), and two secondary skills are offered, picked by the class's
    /// skill weights. A skill with weight 0 is never offered to the class.
    /// </summary>
    public sealed class HeroClassDefinition
    {
        private readonly int[] lowLevelWeights;
        private readonly int[] highLevelWeights;
        private readonly Dictionary<SecondarySkill, int> skillWeights;

        public HeroClassDefinition(string id, string name, string description, IEnumerable<int> lowLevelPrimaryWeights,
            IEnumerable<int> highLevelPrimaryWeights, int highLevelFrom, IReadOnlyDictionary<SecondarySkill, int> skillWeights, int wisdomInterval)
        {
            Id = id;
            Name = name;
            Description = description;
            lowLevelWeights = lowLevelPrimaryWeights.ToArray();
            highLevelWeights = highLevelPrimaryWeights.ToArray();
            HighLevelFrom = highLevelFrom;
            this.skillWeights = skillWeights.ToDictionary(pair => pair.Key, pair => pair.Value);
            WisdomInterval = wisdomInterval;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>The level from which the high-level primary stat weights apply.</summary>
        public int HighLevelFrom { get; }
        /// <summary>Wisdom is offered at the latest on every this many level-ups (while it can still be learned); 0 for never forced.</summary>
        public int WisdomInterval { get; }

        /// <summary>The weight of a skill in the class's level-up offers; 0 for a skill the class never gets.</summary>
        public int SkillWeight(SecondarySkill skill)
        {
            return skillWeights.TryGetValue(skill, out int weight) ? weight : 0;
        }

        /// <summary>The primary stat weights for a level-up to <paramref name="level"/>, in <see cref="PrimaryStat"/> order.</summary>
        public IReadOnlyList<int> PrimaryWeights(int level)
        {
            return level >= HighLevelFrom ? highLevelWeights : lowLevelWeights;
        }
    }
}
