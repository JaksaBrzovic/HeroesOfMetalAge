using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Who a spell affects. Stored in the spell database; new values go at the end.</summary>
    public enum SpellTarget
    {
        /// <summary>One hostile stack.</summary>
        Enemy,
        /// <summary>One of the caster's own stacks.</summary>
        Ally,
        AllEnemies,
        AllAllies,
        /// <summary>The chosen hex and its six neighbours: every stack there, friend or foe.</summary>
        Area,
        /// <summary>Every stack on the battlefield, friend or foe.</summary>
        AllStacks
    }

    /// <summary>What a spell does to each stack it affects. Stored in the spell database; new values go at the end.</summary>
    public enum SpellEffect
    {
        /// <summary>Deals the spell's amount as damage.</summary>
        Damage,
        /// <summary>Heals the top creature of the stack by up to the amount; the dead do not rise.</summary>
        Heal,
        /// <summary>Adds the amount to the stack's attack (negative weakens).</summary>
        Attack,
        /// <summary>Adds the amount to the stack's defense (negative weakens).</summary>
        Defense,
        /// <summary>Adds the amount to the stack's speed (negative slows, never below 1).</summary>
        Speed,
        /// <summary>Melee damage taken shrinks by the amount in percent.</summary>
        Shield,
        /// <summary>The stack always deals its maximum damage.</summary>
        Bless,
        /// <summary>The stack always deals its minimum damage.</summary>
        Curse
    }

    /// <summary>
    /// A battle spell. Damage and healing are <see cref="BaseAmount"/> plus <see cref="AmountPerPower"/> per point of the
    /// caster's spell power; lasting effects hold for <see cref="BaseDuration"/> plus <see cref="DurationPerPower"/> per point of
    /// spell power rounds, at least one.
    /// </summary>
    public sealed class SpellDefinition
    {
        public SpellDefinition(string id, string name, string description, int level, int manaCost, int price, SpellTarget target, SpellEffect effect,
            int baseAmount, int amountPerPower, int baseDuration, int durationPerPower)
        {
            Id = id;
            Name = name;
            Description = description;
            Level = level;
            ManaCost = manaCost;
            Price = price;
            Target = target;
            Effect = effect;
            BaseAmount = baseAmount;
            AmountPerPower = amountPerPower;
            BaseDuration = baseDuration;
            DurationPerPower = durationPerPower;
        }

        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        /// <summary>1 to 5; spells above the hero's Wisdom cannot be learned.</summary>
        public int Level { get; }
        public int ManaCost { get; }
        /// <summary>Gold to learn the spell where it is sold.</summary>
        public int Price { get; }
        public SpellTarget Target { get; }
        public SpellEffect Effect { get; }
        public int BaseAmount { get; }
        public int AmountPerPower { get; }
        public int BaseDuration { get; }
        public int DurationPerPower { get; }

        /// <summary>True for an effect that stays on a stack for some rounds.</summary>
        public bool IsLasting => Effect != SpellEffect.Damage && Effect != SpellEffect.Heal;

        /// <summary>True for a spell that harms the stacks it affects: such a spell can be resisted.</summary>
        public bool IsHarmful => Effect == SpellEffect.Damage || Effect == SpellEffect.Curse
            || ((Effect == SpellEffect.Attack || Effect == SpellEffect.Defense || Effect == SpellEffect.Speed || Effect == SpellEffect.Shield) && BaseAmount < 0);

        /// <summary>True when the caster picks a hex: a single stack or an area.</summary>
        public bool NeedsTarget => Target == SpellTarget.Enemy || Target == SpellTarget.Ally || Target == SpellTarget.Area;

        public int Amount(int spellPower)
        {
            long amount = BaseAmount + (long)AmountPerPower * Math.Max(0, spellPower);
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, amount));
        }

        public int Duration(int spellPower)
        {
            long rounds = BaseDuration + (long)DurationPerPower * Math.Max(0, spellPower);
            return (int)Math.Max(1, Math.Min(int.MaxValue, rounds));
        }
    }

    /// <summary>A lasting spell on a stack. Immutable; recasting the same spell replaces it.</summary>
    public sealed class ActiveSpell
    {
        public ActiveSpell(string spellId, SpellEffect effect, int amount, int roundsLeft)
        {
            SpellId = spellId;
            Effect = effect;
            Amount = amount;
            RoundsLeft = roundsLeft;
        }

        public string SpellId { get; }
        public SpellEffect Effect { get; }
        public int Amount { get; }
        /// <summary>Rounds the effect still lasts, counting the current one.</summary>
        public int RoundsLeft { get; }
    }
}
