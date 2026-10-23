using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Level-ups in the manner of classic hero games. Each level beyond the first grants one point in a primary stat, picked
    /// by the class's weights, and offers two secondary skills to choose from: the first improves a skill the hero knows, the
    /// second is a new skill while a slot is free (each falls back to the other kind when there is none), both picked by
    /// the class's skill weights. Wisdom is offered at the latest every <see cref="HeroClassDefinition.WisdomInterval"/>
    /// level-ups. Levels are granted one at a time: the next waits until the player chose a skill for the previous one.
    /// </summary>
    public sealed class LevelUpRules
    {
        public const int OPTION_COUNT = 2;

        private readonly DefinitionCatalog catalog;
        private readonly CampaignRules rules;

        public LevelUpRules(DefinitionCatalog catalog, CampaignRules rules)
        {
            this.catalog = catalog;
            this.rules = rules;
        }

        private HeroPolicy Policy => catalog.HeroPolicy;

        /// <summary>True when the hero has reached a level whose level-up has not been granted yet.</summary>
        public bool IsDue(CampaignState campaign)
        {
            return campaign.PendingLevelUp == null && campaign.LevelsGranted < rules.Level(campaign);
        }

        /// <summary>
        /// Grants the next due level-up: its primary stat is applied at once and its skill options wait in
        /// <see cref="CampaignState.PendingLevelUp"/>. Does nothing while a level-up waits for a choice.
        /// </summary>
        /// <returns>True when a level-up was granted.</returns>
        public bool Advance(CampaignState campaign, IRandomSource random)
        {
            if (!IsDue(campaign))
            {
                return false;
            }

            HeroClassDefinition heroClass = catalog.ClassOf(campaign.HeroId);
            int level = campaign.LevelsGranted + 1;
            PrimaryStat stat = RollStat(heroClass, level, random);
            List<HeroSkill> options = RollOptions(campaign, heroClass, random);
            campaign.PrimaryGainData[(int)stat]++;
            campaign.LevelsGranted = level;
            campaign.LevelsWithoutWisdom = options.Any(option => option.Skill == SecondarySkill.Wisdom) ? 0 : campaign.LevelsWithoutWisdom + 1;
            campaign.PendingLevelUp = new LevelUpOffer(level, stat, options);
            return true;
        }

        /// <summary>Learns the chosen option of the waiting level-up; with no options any index closes it.</summary>
        public CommandResult Choose(CampaignState campaign, int optionIndex)
        {
            LevelUpOffer offer = campaign.PendingLevelUp;
            if (offer == null)
            {
                return CommandResult.Reject("No level-up waits for a choice.");
            }

            if (offer.Options.Count == 0)
            {
                campaign.PendingLevelUp = null;
                return CommandResult.Ok();
            }

            if (optionIndex < 0 || optionIndex >= offer.Options.Count)
            {
                return CommandResult.Reject("Choose one of the offered skills.");
            }

            HeroSkill chosen = offer.Options[optionIndex];
            int index = campaign.SkillData.FindIndex(skill => skill.Skill == chosen.Skill);
            if (index >= 0)
            {
                campaign.SkillData[index] = chosen;
            }
            else
            {
                campaign.SkillData.Add(chosen);
            }

            campaign.PendingLevelUp = null;
            return CommandResult.Ok();
        }

        private static PrimaryStat RollStat(HeroClassDefinition heroClass, int level, IRandomSource random)
        {
            IReadOnlyList<int> weights = heroClass.PrimaryWeights(level);
            int index = Pick(weights, random);
            return index < 0 ? PrimaryStat.Attack : (PrimaryStat)index;
        }

        private List<HeroSkill> RollOptions(CampaignState campaign, HeroClassDefinition heroClass, IRandomSource random)
        {
            List<SecondarySkill> upgradable = campaign.Skills
                .Where(known => known.Level < SkillLevel.Expert && Policy.HasSkill(known.Skill))
                .Select(known => known.Skill)
                .ToList();
            bool hasFreeSlot = campaign.Skills.Count < Policy.MaximumSkills;
            List<SecondarySkill> fresh = hasFreeSlot
                ? Policy.Skills.Select(skill => skill.Skill).Where(skill => campaign.SkillLevelOf(skill) == SkillLevel.None && heroClass.SkillWeight(skill) > 0).ToList()
                : new List<SecondarySkill>();
            bool isWisdomDue = heroClass.WisdomInterval > 0 && campaign.LevelsWithoutWisdom + 1 >= heroClass.WisdomInterval;

            List<HeroSkill> options = new List<HeroSkill>();
            SecondarySkill? first = upgradable.Count > 0 ? PickSkill(upgradable, heroClass, isWisdomDue, random)
                : fresh.Count > 0 ? PickSkill(fresh, heroClass, isWisdomDue, random)
                : (SecondarySkill?)null;
            if (first.HasValue)
            {
                options.Add(Next(campaign, first.Value));
                upgradable.Remove(first.Value);
                fresh.Remove(first.Value);
                isWisdomDue &= first.Value != SecondarySkill.Wisdom;
            }

            SecondarySkill? second = fresh.Count > 0 ? PickSkill(fresh, heroClass, isWisdomDue, random)
                : upgradable.Count > 0 ? PickSkill(upgradable, heroClass, isWisdomDue, random)
                : (SecondarySkill?)null;
            if (second.HasValue)
            {
                options.Add(Next(campaign, second.Value));
            }

            return options;
        }

        // a due Wisdom among the candidates is taken; otherwise a weighted pick (a known skill the class bans weighs 1)
        private static SecondarySkill PickSkill(List<SecondarySkill> candidates, HeroClassDefinition heroClass, bool isWisdomDue, IRandomSource random)
        {
            if (isWisdomDue && candidates.Contains(SecondarySkill.Wisdom))
            {
                return SecondarySkill.Wisdom;
            }

            int index = Pick(candidates.Select(skill => Math.Max(1, heroClass.SkillWeight(skill))).ToList(), random);
            return candidates[Math.Max(0, index)];
        }

        private static HeroSkill Next(CampaignState campaign, SecondarySkill skill)
        {
            return new HeroSkill(skill, campaign.SkillLevelOf(skill) + 1);
        }

        /// <summary>A weighted random index; -1 when every weight is zero.</summary>
        private static int Pick(IReadOnlyList<int> weights, IRandomSource random)
        {
            long total = weights.Sum(weight => (long)Math.Max(0, weight));
            if (total <= 0)
            {
                return -1;
            }

            double roll = random.NextValue() * total;
            long running = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                running += Math.Max(0, weights[i]);
                if (roll < running)
                {
                    return i;
                }
            }

            return weights.Count - 1;
        }
    }
}
