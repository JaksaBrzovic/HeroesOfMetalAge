using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// One army's hero in a battle: the stats every stack of the side fights with, the secondary skills that act in battle,
    /// and the magic: the spells, the mana left and whether the hero has cast this round. The enemy side has a hero only when
    /// its army has a commander; its attack and defense come from the mission's combat settings.
    /// </summary>
    public sealed class BattleSide
    {
        internal BattleSide(bool isPlayer, string heroName, int attack, int defense, int spellPower, int mana, int morale, int luck, bool hasSpellBook,
            IEnumerable<string> spellIds, SideSkills skills)
        {
            IsPlayer = isPlayer;
            HeroName = heroName;
            Attack = attack;
            Defense = defense;
            SpellPower = spellPower;
            Mana = mana;
            Morale = morale;
            Luck = luck;
            HasSpellBook = hasSpellBook;
            Spells = spellIds.ToList().AsReadOnly();
            Skills = skills;
        }

        public bool IsPlayer { get; }
        /// <summary>The hero leading the side, or null for an army without one.</summary>
        public string HeroName { get; }
        public int Attack { get; }
        public int Defense { get; }
        public int SpellPower { get; }
        public int Mana { get; internal set; }
        public int Morale { get; }
        public int Luck { get; }
        public bool HasSpellBook { get; }
        /// <summary>The spells the side's hero can cast, in spell book order.</summary>
        public IReadOnlyList<string> Spells { get; }
        public SideSkills Skills { get; }
        /// <summary>A hero casts at most once per round.</summary>
        public bool HasCastThisRound { get; internal set; }
    }

    /// <summary>What a side's secondary skills do in battle, already turned into values (see the hero rules).</summary>
    public readonly struct SideSkills
    {
        public SideSkills(double offense, double archery, double armorer, double resistance, double sorcery, int tacticsColumns)
        {
            Offense = offense;
            Archery = archery;
            Armorer = armorer;
            Resistance = resistance;
            Sorcery = sorcery;
            TacticsColumns = tacticsColumns;
        }

        /// <summary>Extra melee damage, as a fraction.</summary>
        public double Offense { get; }
        /// <summary>Extra damage of ranged shots, as a fraction.</summary>
        public double Archery { get; }
        /// <summary>Less damage taken by the side's stacks, as a fraction.</summary>
        public double Armorer { get; }
        /// <summary>Chance that a hostile spell fails against one of the side's stacks.</summary>
        public double Resistance { get; }
        /// <summary>Extra spell damage, as a fraction.</summary>
        public double Sorcery { get; }
        /// <summary>Tactics: how many columns from its edge the side may place its stacks in before the battle.</summary>
        public int TacticsColumns { get; }
    }
}
