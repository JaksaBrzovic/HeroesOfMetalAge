using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Authored portraits and icons by stable ID.</summary>
    public sealed class PresentationAssets
    {
        private readonly IReadOnlyDictionary<string, Sprite> units;
        private readonly IReadOnlyDictionary<string, Sprite> heroes;
        private readonly IReadOnlyDictionary<string, Sprite> artefacts;

        public PresentationAssets(IReadOnlyDictionary<string, Sprite> units, IReadOnlyDictionary<string, Sprite> heroes,
            IReadOnlyDictionary<string, Sprite> artefacts, Sprite town)
        {
            this.units = units;
            this.heroes = heroes;
            this.artefacts = artefacts;
            Town = town;
        }

        public Sprite Town { get; }

        /// <returns>The authored sprite, or null; callers fall back to placeholder art.</returns>
        public Sprite Unit(string id)
        {
            return units.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        public Sprite Hero(string id)
        {
            return heroes.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        public Sprite Artefact(string id)
        {
            return artefacts.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }
    }
}
