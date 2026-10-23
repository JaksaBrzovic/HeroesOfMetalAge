using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// What every screen view needs: the session to command and read, audio settings, authored media and the theme.
    /// Sprite lookups fall back to placeholder art with Unity null checks, so missing or destroyed sprites never hide a marker.
    /// </summary>
    public sealed class CanvasViewContext
    {
        public CanvasViewContext(GameSession session, IAudioControls audio, PresentationAssets media, UiTheme theme)
        {
            Session = session;
            Audio = audio;
            Media = media;
            Theme = theme;
        }

        public GameSession Session { get; }
        public IAudioControls Audio { get; }
        public PresentationAssets Media { get; }
        public UiTheme Theme { get; }

        /// <summary>The hero's figure on the adventure map.</summary>
        public Sprite HeroIcon
        {
            get
            {
                Sprite authored = Media.Hero(Session.Campaign.HeroId);
                return authored != null ? authored : Theme.Hero;
            }
        }

        /// <summary>The hero's face for the hero panel, else the figure.</summary>
        public Sprite HeroPanelPortrait
        {
            get
            {
                Sprite authored = Media.HeroPanelPortrait(Session.Campaign.HeroId);
                return authored != null ? authored : HeroIcon;
            }
        }

        /// <summary>The hero's large picture, else the portrait.</summary>
        public Sprite HeroFullPortrait
        {
            get
            {
                Sprite authored = Media.HeroScreenPortrait(Session.Campaign.HeroId);
                return authored != null ? authored : HeroPanelPortrait;
            }
        }

        public Sprite TownIcon
        {
            get
            {
                Sprite authored = Media.Town;
                return authored != null ? authored : Theme.Town;
            }
        }

        /// <summary>Authored art is shown untinted; placeholder art is tinted gold.</summary>
        public Color HeroTint => Media.Hero(Session.Campaign.HeroId) != null ? Color.white : Theme.Gold;
        public Color TownTint => Media.Town != null ? Color.white : Theme.Gold;

        /// <summary>A unit's figure on the map and the battlefield.</summary>
        public Sprite UnitIcon(string unitId)
        {
            Sprite authored = Media.Unit(unitId);
            return authored != null ? authored : Theme.Unit(Session.Catalog.Unit(unitId).IconKey);
        }

        /// <summary>A unit's picture in the UI: its card, else its figure.</summary>
        public Sprite UnitCard(string unitId)
        {
            Sprite card = Media.UnitCard(unitId);
            return card != null ? card : UnitIcon(unitId);
        }

        public Sprite ArtefactIcon(string artefactId)
        {
            Sprite authored = Media.Artefact(artefactId);
            return authored != null ? authored : Theme.Axe;
        }

        /// <summary>Authored art is shown untinted; placeholder art is tinted by side.</summary>
        public Color UnitTint(string unitId, bool isPlayer = true)
        {
            if (Media.Unit(unitId) != null || Media.UnitCard(unitId) != null)
            {
                return Color.white;
            }

            return isPlayer ? Theme.Gold : Theme.Enemy;
        }

        public Color ArtefactTint(string artefactId)
        {
            return Media.Artefact(artefactId) != null ? Color.white : Theme.Gold;
        }
    }
}
