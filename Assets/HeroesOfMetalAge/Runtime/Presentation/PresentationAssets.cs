using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
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

    /// <summary>A still figure on the adventure map and its height in cells.</summary>
    public readonly struct MapFigure
    {
        public MapFigure(Sprite sprite, float height)
        {
            Sprite = sprite;
            Height = height;
        }

        /// <summary>Null when the object uses the theme's placeholder token.</summary>
        public Sprite Sprite { get; }
        public float Height { get; }
    }

    /// <summary>Authored portraits, icons, walk cycles, map figures and battlefield images by stable ID.</summary>
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
        private readonly IReadOnlyDictionary<string, Sprite> heroScreenPortraits;
        private readonly IReadOnlyDictionary<string, Sprite> heroPanelPortraits;
        private readonly IReadOnlyDictionary<string, Sprite> unitCards;
        private readonly IReadOnlyDictionary<string, MapFigure> objectFigures;
        private readonly IReadOnlyDictionary<string, Sprite> encounterFigures;
        private readonly IReadOnlyDictionary<SecondarySkill, Sprite> skillIcons;
        private readonly IReadOnlyDictionary<string, Sprite> spellIcons;

        public PresentationAssets(IReadOnlyDictionary<string, Sprite> units, IReadOnlyDictionary<string, Sprite> heroes,
            IReadOnlyDictionary<string, Sprite> artefacts, Sprite town, IReadOnlyDictionary<string, FigureAnimation> unitAnimations = null,
            IReadOnlyDictionary<string, FigureAnimation> heroAnimations = null, IReadOnlyDictionary<string, Sprite> battlegrounds = null,
            Sprite defaultBattleground = null, IReadOnlyDictionary<string, Sprite> heroScreenPortraits = null,
            IReadOnlyDictionary<string, MapFigure> objectFigures = null, IReadOnlyDictionary<string, Sprite> encounterFigures = null,
            IReadOnlyDictionary<string, Sprite> heroPanelPortraits = null, IReadOnlyDictionary<string, Sprite> unitCards = null,
            IReadOnlyDictionary<SecondarySkill, Sprite> skillIcons = null, IReadOnlyDictionary<string, Sprite> spellIcons = null)
        {
            this.units = units;
            this.heroes = heroes;
            this.artefacts = artefacts;
            this.unitAnimations = unitAnimations ?? NoAnimations;
            this.heroAnimations = heroAnimations ?? NoAnimations;
            this.battlegrounds = battlegrounds ?? NoSprites;
            this.heroScreenPortraits = heroScreenPortraits ?? NoSprites;
            this.heroPanelPortraits = heroPanelPortraits ?? NoSprites;
            this.unitCards = unitCards ?? NoSprites;
            this.objectFigures = objectFigures ?? new Dictionary<string, MapFigure>();
            this.encounterFigures = encounterFigures ?? NoSprites;
            this.skillIcons = skillIcons ?? new Dictionary<SecondarySkill, Sprite>();
            this.spellIcons = spellIcons ?? NoSprites;
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

        /// <summary>A secondary skill's icon, or null.</summary>
        public Sprite SkillIcon(SecondarySkill skill)
        {
            return skillIcons.TryGetValue(skill, out Sprite sprite) ? sprite : null;
        }

        /// <summary>A spell's icon, or null.</summary>
        public Sprite SpellIcon(string id)
        {
            return spellIcons.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        /// <summary>A unit's picture for the UI, or null to use its figure.</summary>
        public Sprite UnitCard(string id)
        {
            return unitCards.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        /// <summary>The hero's face for the adventure screen's panel, or null to use the figure.</summary>
        public Sprite HeroPanelPortrait(string id)
        {
            return heroPanelPortraits.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        /// <summary>The hero's large picture for the hero screen, or null.</summary>
        public Sprite HeroScreenPortrait(string id)
        {
            return heroScreenPortraits.TryGetValue(id, out Sprite sprite) ? sprite : null;
        }

        /// <summary>A map object's figure; its sprite is null when it uses the placeholder token.</summary>
        public MapFigure ObjectFigure(string id)
        {
            return objectFigures.TryGetValue(id, out MapFigure figure) ? figure : new MapFigure(null, 1.2f);
        }

        /// <summary>The figure an encounter shows on the map instead of its first unit, or null.</summary>
        public Sprite EncounterFigure(string id)
        {
            return encounterFigures.TryGetValue(id, out Sprite sprite) ? sprite : null;
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
