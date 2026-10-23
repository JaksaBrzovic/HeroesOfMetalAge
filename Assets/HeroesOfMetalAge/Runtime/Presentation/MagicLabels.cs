using System;
using System.Globalization;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Player-facing texts for secondary skills, primary stats and spells.</summary>
    public static class MagicLabels
    {
        /// <summary>"ADVANCED LOGISTICS".</summary>
        public static string SkillTitle(HeroPolicy policy, HeroSkill skill)
        {
            return (skill.Level.ToString() + " " + policy.Skill(skill.Skill).Name).ToUpperInvariant();
        }

        /// <summary>What the skill does at its level, in numbers: "+20% movement on land".</summary>
        public static string SkillEffect(HeroPolicy policy, HeroSkill skill)
        {
            double value = policy.Value(skill.Skill, skill.Level);
            switch (skill.Skill)
            {
                case SecondarySkill.Archery:
                    return Percent(value) + " ranged damage";
                case SecondarySkill.Armorer:
                    return "-" + Percent(value).TrimStart('+') + " damage taken";
                case SecondarySkill.Offense:
                    return Percent(value) + " melee damage";
                case SecondarySkill.Diplomacy:
                    return Percent(value).TrimStart('+') + " of willing armies join, surrender "
                        + Percent(policy.SurrenderDiscount(skill.Level)).TrimStart('+') + " cheaper";
                case SecondarySkill.Estates:
                    return "+" + Whole(value) + " gold every day";
                case SecondarySkill.Intelligence:
                    return Percent(value) + " maximum mana";
                case SecondarySkill.Leadership:
                    return "+" + Whole(value) + " morale";
                case SecondarySkill.Logistics:
                    return Percent(value) + " movement on land";
                case SecondarySkill.Luck:
                    return "+" + Whole(value) + " luck";
                case SecondarySkill.Mysticism:
                    return "+" + Whole(value) + " mana every day";
                case SecondarySkill.Pathfinding:
                    return "rough terrain penalty " + Percent(value).Replace('+', '-');
                case SecondarySkill.Resistance:
                    return Percent(value).TrimStart('+') + " chance to resist hostile spells";
                case SecondarySkill.Sorcery:
                    return Percent(value) + " spell damage";
                case SecondarySkill.Tactics:
                    return "place your stacks in the first " + Whole(value) + " columns";
                default:
                    return "learn spells up to level " + Whole(value);
            }
        }

        public static string StatName(PrimaryStat stat)
        {
            return stat == PrimaryStat.SpellPower ? "SPELL POWER" : stat.ToString().ToUpperInvariant();
        }

        /// <summary>"LEVEL 2 / 10 MANA".</summary>
        public static string SpellCost(SpellDefinition spell)
        {
            return "LEVEL " + spell.Level + " / " + spell.ManaCost + " MANA";
        }

        /// <summary>What the spell does at a spell power: "45 damage to one enemy", "+3 attack for 2 rounds to every ally".</summary>
        public static string SpellDescription(SpellDefinition spell, int spellPower)
        {
            int amount = spell.Amount(spellPower);
            int rounds = spell.Duration(spellPower);
            string duration = " for " + rounds + (rounds == 1 ? " round" : " rounds");
            string effect;
            switch (spell.Effect)
            {
                case SpellEffect.Damage:
                    effect = amount + " damage";
                    duration = string.Empty;
                    break;
                case SpellEffect.Heal:
                    effect = "heals " + amount;
                    duration = string.Empty;
                    break;
                case SpellEffect.Shield:
                    effect = (amount >= 0 ? "-" : "+") + Math.Abs(amount) + "% melee damage taken";
                    break;
                case SpellEffect.Bless:
                    effect = "maximum damage";
                    break;
                case SpellEffect.Curse:
                    effect = "minimum damage";
                    break;
                default:
                    effect = (amount >= 0 ? "+" : string.Empty) + amount + " " + spell.Effect.ToString().ToLowerInvariant();
                    break;
            }

            return effect + duration + " " + TargetText(spell.Target);
        }

        private static string TargetText(SpellTarget target)
        {
            switch (target)
            {
                case SpellTarget.Enemy:
                    return "to one enemy stack";
                case SpellTarget.Ally:
                    return "to one of your stacks";
                case SpellTarget.AllEnemies:
                    return "to every enemy stack";
                case SpellTarget.AllAllies:
                    return "to every stack of yours";
                case SpellTarget.Area:
                    return "to every stack in a hex and around it";
                default:
                    return "to every stack on the field";
            }
        }

        private static string Percent(double fraction)
        {
            return "+" + Math.Round(fraction * 100d).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string Whole(double value)
        {
            return Math.Round(value).ToString(CultureInfo.InvariantCulture);
        }
    }
}
