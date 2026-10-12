using System;

namespace HeroesOfMetalAge.Domain
{
    public static class DamageCalculator
    {
        /// <summary>
        /// Damage of one strike. The attack/defense difference scales damage within the policy caps; defending targets and
        /// ranged units fighting in melee take their policy multipliers. Always at least 1.
        /// </summary>
        /// <param name="roll">Random value in [0, 1] choosing between minimum and maximum damage.</param>
        public static int Calculate(UnitDefinition attacker, int count, int heroAttack, UnitDefinition target, int heroDefense, bool isDefending,
            bool isMelee, double roll, CombatPolicy policy)
        {
            long delta = (long)attacker.Attack + heroAttack - target.Defense - heroDefense;
            double multiplier = delta >= 0
                ? 1 + Math.Min(delta, policy.MaximumAttackDelta) * policy.AttackStep
                : 1 - Math.Min(-delta, policy.MaximumDefenseDelta) * policy.DefenseStep;
            double perCreature = attacker.MinimumDamage + (attacker.MaximumDamage - attacker.MinimumDamage) * Math.Max(0, Math.Min(1, roll));
            double damage = count * perCreature * multiplier;
            if (isDefending)
            {
                damage *= policy.DefenseMultiplier;
            }

            if (attacker.IsRanged && isMelee)
            {
                damage *= policy.RangedMeleeMultiplier;
            }

            return (int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(damage, MidpointRounding.ToEven)));
        }
    }
}
