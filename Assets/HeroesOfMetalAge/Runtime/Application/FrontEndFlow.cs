using System;

namespace HeroesOfMetalAge.Application
{
    /// <summary>Where the game is between starting the program and playing the mission.</summary>
    public enum FrontEndPhase
    {
        /// <summary>The main menu is open; the mission waits behind it.</summary>
        MainMenu,
        /// <summary>New Game or Load Game was chosen: the screen and the music go to black.</summary>
        FadingOut,
        /// <summary>A few seconds of black silence.</summary>
        Black,
        /// <summary>The intro's stanzas are typed on black, still in silence (New Game only).</summary>
        Verses,
        /// <summary>The intro's title fades in on black while the map music fades in (New Game only).</summary>
        Title,
        /// <summary>The black burns away and uncovers the mission; input waits until it is gone.</summary>
        Burning,
        /// <summary>The mission is being played.</summary>
        Playing,
        /// <summary>Credits was chosen: its scroll unrolls and the credits screen fades in.</summary>
        OpeningCredits,
        /// <summary>The credits roll until the player clicks.</summary>
        Credits,
        /// <summary>The credits screen fades out to the main menu.</summary>
        ClosingCredits,
        /// <summary>Quit was chosen: the rider leaves and the music fades out.</summary>
        Quitting,
        /// <summary>The program should close now.</summary>
        Quit
    }

    /// <summary>The main menu entry the player picked last.</summary>
    public enum FrontEndChoice
    {
        None,
        NewGame,
        LoadGame,
        Credits,
        Quit
    }

    /// <summary>Lengths of the front end's timed phases, in seconds.</summary>
    public sealed class FrontEndTiming
    {
        /// <summary>Every phase finishes at once.</summary>
        public static readonly FrontEndTiming Instant = new FrontEndTiming(0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);

        /// <param name="fadeOutSeconds">New Game's fade to black.</param>
        /// <param name="loadGameOutSeconds">Load Game's way to black: its text falls into the hole and the camera falls in after it.</param>
        /// <param name="musicFadeInSeconds">How long the music takes to come back, from the title (New Game) or the burn (Load Game).</param>
        public FrontEndTiming(float fadeOutSeconds, float loadGameOutSeconds, float blackSeconds, float burnSeconds, float musicFadeInSeconds,
            float creditsOpenSeconds, float creditsCloseSeconds, float quitSeconds)
        {
            FadeOutSeconds = Math.Max(0f, fadeOutSeconds);
            LoadGameOutSeconds = Math.Max(0f, loadGameOutSeconds);
            BlackSeconds = Math.Max(0f, blackSeconds);
            BurnSeconds = Math.Max(0f, burnSeconds);
            MusicFadeInSeconds = Math.Max(0f, musicFadeInSeconds);
            CreditsOpenSeconds = Math.Max(0f, creditsOpenSeconds);
            CreditsCloseSeconds = Math.Max(0f, creditsCloseSeconds);
            QuitSeconds = Math.Max(0f, quitSeconds);
        }

        public float FadeOutSeconds { get; }
        public float LoadGameOutSeconds { get; }
        public float BlackSeconds { get; }
        public float BurnSeconds { get; }
        public float MusicFadeInSeconds { get; }
        public float CreditsOpenSeconds { get; }
        public float CreditsCloseSeconds { get; }
        public float QuitSeconds { get; }
    }

    /// <summary>
    /// The front end around the mission. New Game: fade to black, black, the intro's verses and title, the burn. Load Game
    /// (for now it starts the mission like New Game): its own way to black, black, the burn without the intro. Credits:
    /// open, roll until a click, close back to the menu. Quit: the rider leaves, then <see cref="FrontEndPhase.Quit"/>. It
    /// only times the phases and reports them; the views draw them, the audio follows them and the composition root starts
    /// the mission when the screen is black and closes the program on Quit.
    /// </summary>
    public sealed class FrontEndFlow
    {
        private readonly FrontEndTiming timing;
        private float elapsed;

        /// <param name="intro">The verses and the title; <see cref="IntroScript.None"/> skips them.</param>
        /// <param name="isMenuShown">False skips the menu and starts in <see cref="FrontEndPhase.Playing"/> (automated runs).</param>
        public FrontEndFlow(FrontEndTiming timing, IntroScript intro, bool isMenuShown)
        {
            this.timing = timing ?? throw new ArgumentNullException(nameof(timing));
            Intro = intro ?? throw new ArgumentNullException(nameof(intro));
            Phase = isMenuShown ? FrontEndPhase.MainMenu : FrontEndPhase.Playing;
        }

        /// <summary>Raised on every phase change, also for phases a zero duration skips at once.</summary>
        public event Action<FrontEndPhase> PhaseChanged;

        public FrontEndPhase Phase { get; private set; }
        /// <summary>The entry picked last; None in the menu.</summary>
        public FrontEndChoice Choice { get; private set; }
        public IntroScript Intro { get; }
        public FrontEndTiming Timing => timing;
        /// <summary>How long the music takes to fade out for the current choice.</summary>
        public float MusicFadeOutSeconds => Choice == FrontEndChoice.Quit ? timing.QuitSeconds
            : Choice == FrontEndChoice.LoadGame ? timing.LoadGameOutSeconds
            : timing.FadeOutSeconds;
        public float MusicFadeInSeconds => timing.MusicFadeInSeconds;
        /// <summary>Seconds since the current phase started; in <see cref="FrontEndPhase.Credits"/> it keeps counting while the credits roll.</summary>
        public float Elapsed => elapsed;
        /// <summary>0 when the current phase starts, 1 when it ends; 1 in the phases that wait.</summary>
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
            return Choose(FrontEndChoice.NewGame, FrontEndPhase.FadingOut);
        }

        /// <summary>For now Load Game starts the mission like New Game, without the intro.</summary>
        /// <returns>False unless the main menu is open.</returns>
        public bool StartLoadGame()
        {
            return Choose(FrontEndChoice.LoadGame, FrontEndPhase.FadingOut);
        }

        /// <returns>False unless the main menu is open.</returns>
        public bool OpenCredits()
        {
            return Choose(FrontEndChoice.Credits, FrontEndPhase.OpeningCredits);
        }

        /// <returns>False unless the credits are rolling; a click while they open or close is ignored.</returns>
        public bool CloseCredits()
        {
            if (Phase != FrontEndPhase.Credits)
            {
                return false;
            }

            Enter(FrontEndPhase.ClosingCredits);
            return true;
        }

        /// <returns>False unless the main menu is open.</returns>
        public bool Quit()
        {
            return Choose(FrontEndChoice.Quit, FrontEndPhase.Quitting);
        }

        /// <summary>Advances the timed phases; driven by the presentation with unscaled frame time.</summary>
        public void Tick(float deltaSeconds)
        {
            if (Phase == FrontEndPhase.MainMenu || Phase == FrontEndPhase.Playing || Phase == FrontEndPhase.Quit)
            {
                return;
            }

            elapsed += Math.Max(0f, deltaSeconds);
            // a long frame may finish several short phases at once; the credits wait for a click
            while (!IsWaiting(Phase) && elapsed >= PhaseSeconds(Phase))
            {
                float rest = elapsed - PhaseSeconds(Phase);
                Enter(Next(Phase));
                elapsed = rest;
            }
        }

        private bool Choose(FrontEndChoice choice, FrontEndPhase phase)
        {
            if (Phase != FrontEndPhase.MainMenu)
            {
                return false;
            }

            Choice = choice;
            Enter(phase);
            return true;
        }

        private FrontEndPhase Next(FrontEndPhase phase)
        {
            switch (phase)
            {
                case FrontEndPhase.FadingOut:
                    return FrontEndPhase.Black;
                case FrontEndPhase.Black:
                    return Choice == FrontEndChoice.LoadGame ? FrontEndPhase.Burning : FrontEndPhase.Verses;
                case FrontEndPhase.Verses:
                    return FrontEndPhase.Title;
                case FrontEndPhase.Title:
                    return FrontEndPhase.Burning;
                case FrontEndPhase.Burning:
                    return FrontEndPhase.Playing;
                case FrontEndPhase.OpeningCredits:
                    return FrontEndPhase.Credits;
                case FrontEndPhase.ClosingCredits:
                    return FrontEndPhase.MainMenu;
                case FrontEndPhase.Quitting:
                    return FrontEndPhase.Quit;
                default:
                    return phase;
            }
        }

        private static bool IsWaiting(FrontEndPhase phase)
        {
            return phase == FrontEndPhase.MainMenu || phase == FrontEndPhase.Playing || phase == FrontEndPhase.Credits || phase == FrontEndPhase.Quit;
        }

        private float PhaseSeconds(FrontEndPhase phase)
        {
            switch (phase)
            {
                case FrontEndPhase.FadingOut:
                    return Choice == FrontEndChoice.LoadGame ? timing.LoadGameOutSeconds : timing.FadeOutSeconds;
                case FrontEndPhase.Black:
                    return timing.BlackSeconds;
                case FrontEndPhase.Verses:
                    return Intro.VersesSeconds;
                case FrontEndPhase.Title:
                    return Intro.TitleSeconds;
                case FrontEndPhase.Burning:
                    return timing.BurnSeconds;
                case FrontEndPhase.OpeningCredits:
                    return timing.CreditsOpenSeconds;
                case FrontEndPhase.ClosingCredits:
                    return timing.CreditsCloseSeconds;
                case FrontEndPhase.Quitting:
                    return timing.QuitSeconds;
                default:
                    return 0f;
            }
        }

        private void Enter(FrontEndPhase phase)
        {
            Phase = phase;
            elapsed = 0f;
            if (phase == FrontEndPhase.MainMenu)
            {
                Choice = FrontEndChoice.None;
            }

            PhaseChanged?.Invoke(phase);
        }
    }
}
