using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// One battle spell. Damage and healing are the base amount plus the amount per point of the caster's spell power; a
    /// lasting effect holds for the base duration plus the duration per point of spell power rounds (at least one) and adds
    /// its amount (attack, defense, speed; shield: percent less melee damage).
    /// </summary>
    [Serializable]
    public sealed class SpellEntry
    {
        [Tooltip("Stable ID used by saves, shops and commanders.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [TextArea]
        [SerializeField] private string description;
        [Tooltip("Shown in the spell book and the shop; empty uses the placeholder art.")]
        [SerializeField] private Sprite icon;
        [Tooltip("1 to 5; levels above the hero's Wisdom cannot be learned.")]
        [Range(1, 5)]
        [SerializeField] private int level = 1;
        [Min(0)]
        [SerializeField] private int manaCost = 5;
        [Tooltip("Gold to learn the spell where it is sold.")]
        [Min(0)]
        [SerializeField] private int price = 150;
        [SerializeField] private SpellTarget target;
        [SerializeField] private SpellEffect effect;
        [SerializeField] private int baseAmount;
        [SerializeField] private int amountPerPower;
        [Min(0)]
        [SerializeField] private int baseDuration;
        [Min(0)]
        [SerializeField] private int durationPerPower = 1;

        public SpellEntry()
        {
        }

        internal SpellEntry(string id, string displayName, string description, int level, int manaCost, int price, SpellTarget target, SpellEffect effect,
            int baseAmount, int amountPerPower, int baseDuration, int durationPerPower)
        {
            this.id = id;
            this.displayName = displayName;
            this.description = description;
            this.level = level;
            this.manaCost = manaCost;
            this.price = price;
            this.target = target;
            this.effect = effect;
            this.baseAmount = baseAmount;
            this.amountPerPower = amountPerPower;
            this.baseDuration = baseDuration;
            this.durationPerPower = durationPerPower;
        }

        public string Id => id;
        public string DisplayName => displayName;

        public Sprite Icon
        {
            get => icon;
            internal set => icon = value;
        }

        public int Level => level;
        public int ManaCost => manaCost;
        public int Price => price;
        public SpellTarget Target => target;
        public SpellEffect Effect => effect;

        public SpellDefinition ToDefinition()
        {
            return new SpellDefinition(id, displayName, description, level, manaCost, price, target, effect, baseAmount, amountPerPower, baseDuration,
                durationPerPower);
        }
    }

    /// <summary>Authored battle spells, in spell book order; validated and projected into the gameplay catalog at startup.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Spell Database")]
    public sealed class SpellDatabaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<SpellEntry> spells = new List<SpellEntry>();

        public IReadOnlyList<SpellEntry> Spells => spells;

        internal void SetSpells(IEnumerable<SpellEntry> entries)
        {
            spells = entries.ToList();
        }
    }
}
