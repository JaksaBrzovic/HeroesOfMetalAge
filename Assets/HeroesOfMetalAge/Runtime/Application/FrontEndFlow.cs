using System;

namespace HeroesOfMetalAge.Application
{
    /// <summary>Where the game is between starting the program and playing the mission.</summary>
    public enum FrontEndPhase
    {
        /// <summary>The main menu is open; the mission waits behind it.</summary>
        MainMenu,
        /// <summary>New Game was chosen: the screen and the music fade to black.</summary>
        FadingOut,
        /// <summary>A few seconds of black silence.</summary>
        Black,
        /// <summary>The intro's stanzas are typed on black, still in silence.</summary>
        Verses,
        /// <summary>The intro's title fades in on black while the map music fades in.</summary>
        Title,
        /// <summary>The black with the title burns away and uncovers the mission; input waits until it is gone.</summary>
        Burning,
        /// <summary>The mission is being played.</summary>
        Playing
    }

    /// <summary>
    /// The sequence from the main menu into the mission: fade to black, black, the intro's verses and title, the burn. It
    /// only times the phases and reports them; the views draw them, the audio follows them and the composition root starts
    /// the mission when the screen is black.
    /// </summary>
    public sealed class FrontEndFlow
    {
        private readonly float fadeOutSeconds;
        private readonly float blackSeconds;
        private readonly float burnSeconds;
        private readonly float musicFadeInSeconds;
        private float elapsed;

        /// <param name="intro">The verses and the title; <see cref="IntroScript.None"/> skips them.</param>
        /// <param name="musicFadeInSeconds">How long the music takes to come back, starting with the title.</param>
        /// <param name="isMenuShown">False skips the menu and starts in <see cref="FrontEndPhase.Playing"/> (automated runs).</param>
        public FrontEndFlow(float fadeOutSeconds, float blackSeconds, IntroScript intro, float burnSeconds, float musicFadeInSeconds, bool isMenuShown)
        {
            Intro = intro ?? throw new ArgumentNullException(nameof(intro));
            this.fadeOutSeconds = Math.Max(0f, fadeOutSeconds);
            this.blackSeconds = Math.Max(0f, blackSeconds);
            this.burnSeconds = Math.Max(0f, burnSeconds);
            this.musicFadeInSeconds = Math.Max(0f, musicFadeInSeconds);
            Phase = isMenuShown ? FrontEndPhase.MainMenu : FrontEndPhase.Playing;
        }

        /// <summary>Raised on every phase change, also for phases a zero duration skips at once.</summary>
        public event Action<FrontEndPhase> PhaseChanged;

        public FrontEndPhase Phase { get; private set; }
        public IntroScript Intro { get; }
        public float FadeOutSeconds => fadeOutSeconds;
        public float MusicFadeInSeconds => musicFadeInSeconds;
        /// <summary>Seconds since the current phase started.</summary>
        public float Elapsed => elapsed;
        /// <summary>0 when the current phase starts, 1 when it ends; 1 in the main menu and while playing.</summary>
        public float Progress => PhaseSeconds(Phase) > 0f ? Math.Min(1f, elapsed / PhaseSeconds(Phase)) : 1f;
        /// <summary>How much of the screen the black covers: 0 in the menu and while playing, 1 from black to the burn.</summary>
        public float Blackness
        {
            get
            {
                switch (Phase)
                {
                    case FrontEndPhase.FadingOut:
                        return Progress;
                    case FrontEndPhase.Black:
                    case FrontEndPhase.Verses:
                    case FrontEndPhase.Title:
                        return 1f;
                    case FrontEndPhase.Burning:
                        return 1f - Progress;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>True only while the mission is being played; the menu and the transition take no gameplay input.</summary>
        public bool IsPlaying => Phase == FrontEndPhase.Playing;

        /// <returns>False unless the main menu is open; a second click during the transition is ignored.</returns>
        public bool StartNewGame()
        {
            if (Phase != FrontEndPhase.MainMenu)
            {
                return false;
            }

            Enter(FrontEndPhase.FadingOut);
            return true;
        }

        /// <summary>Advances the transition; driven by the presentation with unscaled frame time.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Phase == FrontEndPhase.MainMenu || Phase == FrontEndPhase.Playing)
            {
                return;
            }

            elapsed += Math.Max(0f, deltaSeconds);
            // a long frame may finish several short phases at once
            while (Phase != FrontEndPhase.Playing && elapsed >= PhaseSeconds(Phase))
            {
                float rest = elapsed - PhaseSeconds(Phase);
                Enter(Phase + 1);
                elapsed = rest;
            }
        }

        private float PhaseSeconds(FrontEndPhase phase)
        {
            switch (phase)
            {
                case FrontEndPhase.FadingOut:
                    return fadeOutSeconds;
                case FrontEndPhase.Black:
                    return blackSeconds;
                case FrontEndPhase.Verses:
                    return Intro.VersesSeconds;
                case FrontEndPhase.Title:
                    return Intro.TitleSeconds;
                case FrontEndPhase.Burning:
                    return burnSeconds;
                default:
                    return 0f;
            }
        }

        private void Enter(FrontEndPhase phase)
        {
            Phase = phase;
            elapsed = 0f;
            PhaseChanged?.Invoke(phase);
        }
    }
}
