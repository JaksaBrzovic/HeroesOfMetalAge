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

        public DefinitionCatalog(IEnumerable<UnitDefinition> units, IEnumerable<ArtefactDefinition> artefacts, IEnumerable<HeroDefinition> heroes,
            MissionDefinition mission)
        {
            Units = units.ToList().AsReadOnly();
            Artefacts = artefacts.ToList().AsReadOnly();
            Heroes = heroes.ToList().AsReadOnly();
            Mission = mission;
            this.units = Units.ToDictionary(unit => unit.Id, StringComparer.Ordinal);
            this.artefacts = Artefacts.ToDictionary(artefact => artefact.Id, StringComparer.Ordinal);
            this.heroes = Heroes.ToDictionary(hero => hero.Id, StringComparer.Ordinal);
        }

        public IReadOnlyList<UnitDefinition> Units { get; }
        public IReadOnlyList<ArtefactDefinition> Artefacts { get; }
        public IReadOnlyList<HeroDefinition> Heroes { get; }
        public MissionDefinition Mission { get; }

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
    }
}
