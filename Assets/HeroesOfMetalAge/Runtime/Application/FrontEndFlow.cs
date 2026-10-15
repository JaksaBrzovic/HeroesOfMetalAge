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
        /// <summary>The intro plays on black.</summary>
        Intro,
        /// <summary>The mission fades in from black; input waits until it is fully visible.</summary>
        FadingIn,
        /// <summary>The mission is being played.</summary>
        Playing
    }

    /// <summary>
    /// The sequence from the main menu into the mission: fade to black, black, intro, fade in. It only times the phases and
    /// reports them; the views draw them, the audio follows them and the composition root starts the mission when the
    /// screen is black.
    /// </summary>
    public sealed class FrontEndFlow
    {
        private readonly float fadeOutSeconds;
        private readonly float blackSeconds;
        private readonly float introSeconds;
        private readonly float fadeInSeconds;
        private float elapsed;

        /// <param name="isMenuShown">False skips the menu and starts in <see cref="FrontEndPhase.Playing"/> (automated runs).</param>
        public FrontEndFlow(float fadeOutSeconds, float blackSeconds, float introSeconds, float fadeInSeconds, bool isMenuShown)
        {
            this.fadeOutSeconds = Math.Max(0f, fadeOutSeconds);
            this.blackSeconds = Math.Max(0f, blackSeconds);
            this.introSeconds = Math.Max(0f, introSeconds);
            this.fadeInSeconds = Math.Max(0f, fadeInSeconds);
            Phase = isMenuShown ? FrontEndPhase.MainMenu : FrontEndPhase.Playing;
        }

        /// <summary>Raised on every phase change, also for phases a zero duration skips at once.</summary>
        public event Action<FrontEndPhase> PhaseChanged;

        public FrontEndPhase Phase { get; private set; }
        public float FadeOutSeconds => fadeOutSeconds;
        public float FadeInSeconds => fadeInSeconds;
        /// <summary>0 when the current phase starts, 1 when it ends; 1 in the main menu and while playing.</summary>
        public float Progress => PhaseSeconds(Phase) > 0f ? Math.Min(1f, elapsed / PhaseSeconds(Phase)) : 1f;
        /// <summary>How much of the screen the black covers: 0 in the menu and while playing, 1 on black and during the intro.</summary>
        public float Blackness
        {
            get
            {
                switch (Phase)
                {
                    case FrontEndPhase.FadingOut:
                        return Progress;
                    case FrontEndPhase.Black:
                    case FrontEndPhase.Intro:
                        return 1f;
                    case FrontEndPhase.FadingIn:
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
                case FrontEndPhase.Intro:
                    return introSeconds;
                case FrontEndPhase.FadingIn:
                    return fadeInSeconds;
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
