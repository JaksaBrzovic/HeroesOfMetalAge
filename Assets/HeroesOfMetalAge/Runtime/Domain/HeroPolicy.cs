using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Experience needed for each hero level: the authored thresholds first, then every further level costs the previous
    /// step times <see cref="Growth"/>, up to <see cref="MaximumLevel"/>.
    /// </summary>
    public sealed class ExperienceTable
    {
        private readonly long[] thresholds;

        /// <param name="levelThresholds">Total experience to reach level 2, 3, ... in increasing order; at least one.</param>
        public ExperienceTable(IEnumerable<int> levelThresholds, double growth, int maximumLevel)
        {
            List<int> authored = levelThresholds.ToList();
            if (authored.Count == 0 || maximumLevel < 2)
            {
                throw new ArgumentException("An experience table needs at least one threshold and two levels.");
            }

            Growth = growth;
            MaximumLevel = maximumLevel;
            thresholds = new long[maximumLevel + 1];
            long previous = 0;
            double step = authored[0];
            for (int level = 2; level <= maximumLevel; level++)
            {
                int index = level - 2;
                long threshold;
                if (index < authored.Count)
                {
                    threshold = authored[index];
                    step = threshold - previous;
                }
                else
                {
                    step *= growth;
                    threshold = (long)Math.Min(long.MaxValue / 2, previous + Math.Round(step));
                }

                thresholds[level] = threshold;
                previous = threshold;
            }
        }

        public double Growth { get; }
        public int MaximumLevel { get; }

        /// <summary>Total experience needed to reach a level; 0 for level 1.</summary>
        public long Threshold(int level)
        {
            return level <= 1 ? 0 : thresholds[Math.Min(level, MaximumLevel)];
        }

        public int Level(long experience)
        {
            int level = 1;
            while (level < MaximumLevel && experience >= thresholds[level + 1])
            {
                level++;
            }

            return level;
        }
    }

    /// <summary>
    /// The rules of hero growth and magic, from the hero rules asset: experience, the secondary skills and their values per
    /// level, luck, mana, Wisdom and the diplomacy and surrender prices.
    /// </summary>
    public sealed class HeroPolicy
    {
        private readonly Dictionary<SecondarySkill, SkillDefinition> skills;

        public HeroPolicy(ExperienceTable experience, int maximumSkills, IEnumerable<SkillDefinition> skillDefinitions, double luckChance,
            int manaPerKnowledge, int manaRegeneration, int spellLevelWithoutWisdom, bool isManaRestoredInTown, int unpenalizedTerrainCost,
            double joinStrengthRatio, double joinCostMultiplier, double surrenderCostMultiplier, IEnumerable<double> surrenderDiscounts)
        {
            Experience = experience;
            MaximumSkills = maximumSkills;
            Skills = skillDefinitions.ToList().AsReadOnly();
            skills = Skills.ToDictionary(skill => skill.Skill);
            LuckChance = luckChance;
            ManaPerKnowledge = manaPerKnowledge;
            ManaRegeneration = manaRegeneration;
            SpellLevelWithoutWisdom = spellLevelWithoutWisdom;
            IsManaRestoredInTown = isManaRestoredInTown;
            UnpenalizedTerrainCost = unpenalizedTerrainCost;
            JoinStrengthRatio = joinStrengthRatio;
            JoinCostMultiplier = joinCostMultiplier;
            SurrenderCostMultiplier = surrenderCostMultiplier;
            SurrenderDiscounts = surrenderDiscounts.ToList().AsReadOnly();
        }

        public ExperienceTable Experience { get; }
        /// <summary>How many secondary skills a hero can know.</summary>
        public int MaximumSkills { get; }
        /// <summary>Every secondary skill the game knows; a skill without a definition is never offered.</summary>
        public IReadOnlyList<SkillDefinition> Skills { get; }
        /// <summary>Chance per point of luck that a strike deals double damage (good luck) or half damage (bad luck).</summary>
        public double LuckChance { get; }
        public int ManaPerKnowledge { get; }
        /// <summary>Mana regained every day before Mysticism.</summary>
        public int ManaRegeneration { get; }
        /// <summary>The highest spell level a hero without Wisdom can learn.</summary>
        public int SpellLevelWithoutWisdom { get; }
        /// <summary>Entering the player's own town restores the hero's mana.</summary>
        public bool IsManaRestoredInTown { get; }
        /// <summary>Pathfinding cuts only what a terrain costs above this (the cost of plain ground).</summary>
        public int UnpenalizedTerrainCost { get; }
        /// <summary>Diplomacy: a hostile army considers joining only when the hero's army is at least this many times stronger.</summary>
        public double JoinStrengthRatio { get; }
        /// <summary>Diplomacy: an army that joins asks this share of its recruitment price.</summary>
        public double JoinCostMultiplier { get; }
        /// <summary>Surrendering costs this share of the surviving army's recruitment price, before Diplomacy's discount.</summary>
        public double SurrenderCostMultiplier { get; }
        /// <summary>Diplomacy's discount on surrendering at Basic, Advanced and Expert, as fractions.</summary>
        public IReadOnlyList<double> SurrenderDiscounts { get; }

        public bool HasSkill(SecondarySkill skill)
        {
            return skills.ContainsKey(skill);
        }

        public SkillDefinition Skill(SecondarySkill skill)
        {
            return skills[skill];
        }

        /// <summary>A skill's value at a level; 0 for an unknown level or a skill without a definition.</summary>
        public double Value(SecondarySkill skill, SkillLevel level)
        {
            return skills.TryGetValue(skill, out SkillDefinition definition) ? definition.Value(level) : 0d;
        }

        public double SurrenderDiscount(SkillLevel diplomacy)
        {
            return diplomacy == SkillLevel.None ? 0d : SurrenderDiscounts[(int)diplomacy - 1];
        }
    }
}
