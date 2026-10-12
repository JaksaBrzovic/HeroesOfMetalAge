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

        public Sprite HeroIcon
        {
            get
            {
                Sprite authored = Media.Hero(Session.Campaign.HeroId);
                return authored != null ? authored : Theme.Hero;
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

        public Sprite UnitIcon(string unitId)
        {
            Sprite authored = Media.Unit(unitId);
            return authored != null ? authored : Theme.Unit(Session.Catalog.Unit(unitId).IconKey);
        }

        public Sprite ArtefactIcon(string artefactId)
        {
            Sprite authored = Media.Artefact(artefactId);
            return authored != null ? authored : Theme.Axe;
        }

        /// <summary>Authored art is shown untinted; placeholder art is tinted by side.</summary>
        public Color UnitTint(string unitId, bool isPlayer = true)
        {
            if (Media.Unit(unitId) != null)
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
