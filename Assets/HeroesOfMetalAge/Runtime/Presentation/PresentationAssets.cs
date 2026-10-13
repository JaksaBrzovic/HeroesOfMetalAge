using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>A right-facing walk cycle and its speed; empty for still art.</summary>
    public readonly struct FigureAnimation
    {
        public FigureAnimation(IReadOnlyList<Sprite> frames, float framesPerSecond)
        {
            Frames = frames ?? Array.Empty<Sprite>();
            FramesPerSecond = framesPerSecond;
        }

        public IReadOnlyList<Sprite> Frames { get; }
        public float FramesPerSecond { get; }
    }

    /// <summary>Authored portraits, icons, walk cycles and battlefield images by stable ID.</summary>
    public sealed class PresentationAssets
    {
        private static readonly IReadOnlyDictionary<string, FigureAnimation> NoAnimations = new Dictionary<string, FigureAnimation>();
        private static readonly IReadOnlyDictionary<string, Sprite> NoSprites = new Dictionary<string, Sprite>();

        private readonly IReadOnlyDictionary<string, Sprite> units;
        private readonly IReadOnlyDictionary<string, Sprite> heroes;
        private readonly IReadOnlyDictionary<string, Sprite> artefacts;
        private readonly IReadOnlyDictionary<string, FigureAnimation> unitAnimations;
        private readonly IReadOnlyDictionary<string, FigureAnimation> heroAnimations;
        private readonly IReadOnlyDictionary<string, Sprite> battlegrounds;

        public PresentationAssets(IReadOnlyDictionary<string, Sprite> units, IReadOnlyDictionary<string, Sprite> heroes,
            IReadOnlyDictionary<string, Sprite> artefacts, Sprite town, IReadOnlyDictionary<string, FigureAnimation> unitAnimations = null,
            IReadOnlyDictionary<string, FigureAnimation> heroAnimations = null, IReadOnlyDictionary<string, Sprite> battlegrounds = null,
            Sprite defaultBattleground = null)
        {
            this.units = units;
            this.heroes = heroes;
            this.artefacts = artefacts;
            this.unitAnimations = unitAnimations ?? NoAnimations;
            this.heroAnimations = heroAnimations ?? NoAnimations;
            this.battlegrounds = battlegrounds ?? NoSprites;
            Town = town;
            DefaultBattleground = defaultBattleground;
        }

        public Sprite Town { get; }
        /// <summary>The mission's battlefield image, or null to keep the Battlefield prefab's own.</summary>
        public Sprite DefaultBattleground { get; }

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

        public FigureAnimation UnitAnimation(string id)
        {
            return unitAnimations.TryGetValue(id, out FigureAnimation animation) ? animation : default;
        }

        public FigureAnimation HeroAnimation(string id)
        {
            return heroAnimations.TryGetValue(id, out FigureAnimation animation) ? animation : default;
        }

        /// <returns>The encounter's battlefield image, else the mission's, else null.</returns>
        public Sprite Battleground(string encounterId)
        {
            return battlegrounds.TryGetValue(encounterId, out Sprite sprite) && sprite != null ? sprite : DefaultBattleground;
        }
    }
}
