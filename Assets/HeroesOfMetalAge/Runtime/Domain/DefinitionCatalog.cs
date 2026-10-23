using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Read-only lookup of every validated definition, keyed by stable ID.</summary>
    public sealed class DefinitionCatalog
    {
        private readonly Dictionary<string, UnitDefinition> units;
        private readonly Dictionary<string, ArtefactDefinition> artefacts;
        private readonly Dictionary<string, HeroDefinition> heroes;
        private readonly Dictionary<string, HeroClassDefinition> classes;
        private readonly Dictionary<string, SpellDefinition> spells;

        public DefinitionCatalog(IEnumerable<UnitDefinition> units, IEnumerable<ArtefactDefinition> artefacts, IEnumerable<HeroDefinition> heroes,
            MissionDefinition mission, HeroPolicy heroPolicy, IEnumerable<HeroClassDefinition> classes, IEnumerable<SpellDefinition> spells)
        {
            Units = units.ToList().AsReadOnly();
            Artefacts = artefacts.ToList().AsReadOnly();
            Heroes = heroes.ToList().AsReadOnly();
            Classes = classes.ToList().AsReadOnly();
            Spells = spells.ToList().AsReadOnly();
            Mission = mission;
            HeroPolicy = heroPolicy;
            this.units = Units.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
            this.artefacts = Artefacts.ToDictionary(artefact => artefact.Id, StringComparer.Ordinal);
            this.heroes = Heroes.ToDictionary(hero => hero.Id, StringComparer.Ordinal);
            this.classes = Classes.ToDictionary(heroClass => heroClass.Id, StringComparer.Ordinal);
            this.spells = Spells.ToDictionary(spell => spell.Id, StringComparer.Ordinal);
        }

        public IReadOnlyList<UnitDefinition> Units { get; }
        public IReadOnlyList<ArtefactDefinition> Artefacts { get; }
        public IReadOnlyList<HeroDefinition> Heroes { get; }
        public IReadOnlyList<HeroClassDefinition> Classes { get; }
        /// <summary>Every spell, in authored order (the order of the spell book).</summary>
        public IReadOnlyList<SpellDefinition> Spells { get; }
        public MissionDefinition Mission { get; }
        /// <summary>Experience, secondary skills, luck, mana and the other rules of hero growth.</summary>
        public HeroPolicy HeroPolicy { get; }

        public UnitDefinition Unit(string id)
        {
            return units[id];
        }

        public ArtefactDefinition Artefact(string id)
        {
            return artefacts[id];
        }

        public HeroDefinition Hero(string id)
        {
            return heroes[id];
        }

        public HeroClassDefinition Class(string id)
        {
            return classes[id];
        }

        /// <summary>The class of a hero.</summary>
        public HeroClassDefinition ClassOf(string heroId)
        {
            return classes[Hero(heroId).ClassId];
        }

        public SpellDefinition Spell(string id)
        {
            return spells[id];
        }

        public bool HasUnit(string id)
        {
            return id != null && units.ContainsKey(id);
        }

        public bool HasArtefact(string id)
        {
            return id != null && artefacts.ContainsKey(id);
        }

        public bool HasHero(string id)
        {
            return id != null && heroes.ContainsKey(id);
        }

        public bool HasClass(string id)
        {
            return id != null && classes.ContainsKey(id);
        }

        public bool HasSpell(string id)
        {
            return id != null && spells.ContainsKey(id);
        }
    }
}
