using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>What heroes, skills, luck and spells change about one strike. The default changes nothing.</summary>
    public readonly struct StrikeModifiers
    {
        public StrikeModifiers(double damageBonus, double damageReduction, double shieldReduction, int luck, bool isMaximumDamage, bool isMinimumDamage)
        {
            DamageBonus = damageBonus;
            DamageReduction = damageReduction;
            ShieldReduction = shieldReduction;
            Luck = luck;
            IsMaximumDamage = isMaximumDamage;
            IsMinimumDamage = isMinimumDamage;
        }

        /// <summary>Offense or Archery: added to the damage as a fraction.</summary>
        public double DamageBonus { get; }
        /// <summary>The target's Armorer: removed from the damage as a fraction.</summary>
        public double DamageReduction { get; }
        /// <summary>A shield spell on the target: removed from melee damage as a fraction.</summary>
        public double ShieldReduction { get; }
        /// <summary>1 doubles the damage (good luck), -1 halves it (bad luck).</summary>
        public int Luck { get; }
        /// <summary>Bless: every creature deals its maximum damage.</summary>
        public bool IsMaximumDamage { get; }
        /// <summary>Curse: every creature deals its minimum damage.</summary>
        public bool IsMinimumDamage { get; }
    }

    public static class DamageCalculator
    {
        /// <summary>
        /// Damage of one strike. The attack/defense difference scales damage within the policy caps; defending targets and
        /// ranged units fighting in melee take their policy multipliers; skills, luck and spells apply their
        /// <paramref name="modifiers"/> (bonuses add up with good luck, reductions multiply). Always at least 1.
        /// </summary>
        /// <param name="roll">Random value in [0, 1] choosing between minimum and maximum damage.</param>
        public static int Calculate(UnitDefinition attacker, int count, int heroAttack, UnitDefinition target, int heroDefense, bool isDefending,
            bool isMelee, double roll, CombatPolicy policy, StrikeModifiers modifiers = default)
        {
            long delta = (long)attacker.Attack + heroAttack - target.Defense - heroDefense;
            double multiplier = delta >= 0
                ? 1 + Math.Min(delta, policy.MaximumAttackDelta) * policy.AttackStep
                : 1 - Math.Min(-delta, policy.MaximumDefenseDelta) * policy.DefenseStep;
            // bless and curse together cancel out
            double fraction = modifiers.IsMaximumDamage == modifiers.IsMinimumDamage ? Math.Max(0, Math.Min(1, roll))
                : modifiers.IsMaximumDamage ? 1d : 0d;
            double perCreature = attacker.MinimumDamage + (attacker.MaximumDamage - attacker.MinimumDamage) * fraction;
            double damage = count * perCreature * multiplier;
            if (isDefending)
            {
                damage *= policy.DefenseMultiplier;
            }

            if (attacker.IsRanged && isMelee)
            {
                damage *= policy.RangedMeleeMultiplier;
            }

            damage *= 1 + modifiers.DamageBonus + (modifiers.Luck > 0 ? 1d : 0d);
            if (modifiers.Luck < 0)
            {
                damage *= 0.5;
            }

            damage *= Math.Max(0, 1 - modifiers.DamageReduction);
            damage *= Math.Max(0, 1 - modifiers.ShieldReduction);
            return (int)Math.Max(1, Math.Min(int.MaxValue, Math.Round(damage, MidpointRounding.ToEven)));
        }
    }
}
