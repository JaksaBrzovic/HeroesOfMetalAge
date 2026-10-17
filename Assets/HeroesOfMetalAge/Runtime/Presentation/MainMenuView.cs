using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The main menu over the world-themed background, painted in depth layers seen through a camera that follows the
    /// mouse. New Game starts the intro and the mission; Load Game (for now the mission without the intro) lets its title
    /// fall into the hell hole beneath it, then the camera falls into the hole, slowly at first and ever faster; Credits
    /// unrolls its scroll and opens the credits; Quit sends the rider off and closes the program. The entries' effects
    /// only show the <see cref="FrontEndFlow"/>'s phases.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The background in depth layers with the drifting camera.")]
        [SerializeField] private ParallaxView parallax;
        [SerializeField] private NewGameButtonView newGameButton;
        [SerializeField] private MenuButtonView loadGame;
        [SerializeField] private MenuButtonView credits;
        [SerializeField] private MenuButtonView quit;

        [Header("Effects")]
        [Tooltip("The Load Game title, falling into the hole.")]
        [SerializeField] private CrumbleEffect loadGameCrumble;
        [Tooltip("The hole under Load Game; the camera falls into its centre.")]
        [SerializeField] private RectTransform loadGameHole;
        [Tooltip("Everything the camera sees: scaled and turned around the hole as it falls in.")]
        [SerializeField] private RectTransform designFrame;
        [SerializeField] private ScrollUnrollView creditsScroll;
        [SerializeField] private ChopperView quitChopper;
        [Tooltip("Share of Load Game's way to black over which its title falls into the hole.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float crumbleEnd = 0.5f;
        [Tooltip("Share of the credits' opening over which the scroll unrolls.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float unrollEnd = 0.4f;

        [Header("Falling into the hole")]
        [Tooltip("Share of Load Game's way to black after which the camera starts to fall.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float fallStart = 0.1f;
        [Tooltip("How much larger the hole is at the end of the fall; its black middle then fills the screen.")]
        [Min(1f)]
        [SerializeField] private float fallZoom = 40f;
        [Tooltip("Share of the fall over which the hole moves to the middle of the screen.")]
        [Range(0.05f, 1f)]
        [SerializeField] private float centringEnd = 0.5f;
        [Tooltip("Degrees the view turns by the end of the fall, as if sucked into a whirl.")]
        [SerializeField] private float swirlDegrees = 25f;

        private FrontEndFlow flow;
        private IAudioControls audioControls;
        private Vector2 frameHome;
        private bool isFalling;

        public ParallaxView Parallax => parallax;
        public NewGameButtonView NewGameButton => newGameButton;
        public MenuButtonView LoadGameButton => loadGame;
        public MenuButtonView CreditsButton => credits;
        public MenuButtonView QuitButton => quit;
        public CrumbleEffect LoadGameCrumble => loadGameCrumble;
        public RectTransform LoadGameHole => loadGameHole;
        public RectTransform DesignFrame => designFrame;
        public ScrollUnrollView CreditsScroll => creditsScroll;
        public ChopperView QuitChopper => quitChopper;

        /// <exception cref="InvalidOperationException">Called twice, or a reference is missing on the FrontEnd prefab.</exception>
        public void Initialize(FrontEndFlow frontEndFlow, IAudioControls audio)
        {
            if (flow != null)
            {
                throw new InvalidOperationException("Main menu was initialized twice.");
            }

            if (canvasGroup == null || parallax == null || newGameButton == null || loadGame == null || credits == null || quit == null || loadGameCrumble == null
                || loadGameHole == null || designFrame == null || creditsScroll == null || quitChopper == null)
            {
                throw new InvalidOperationException($"MainMenuView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            flow = frontEndFlow;
            audioControls = audio;
            frameHome = designFrame.anchoredPosition;
            parallax.Initialize();
            newGameButton.Initialize();
            newGameButton.Hovered += HandleNewGameHovered;
            newGameButton.Clicked += HandleNewGameClicked;
            loadGame.Initialize();
            loadGame.Clicked += HandleLoadGameClicked;
            credits.Initialize();
            credits.Clicked += HandleCreditsClicked;
            quit.Initialize();
            quit.Clicked += HandleQuitClicked;
            creditsScroll.Initialize();
            quitChopper.Initialize();
            flow.PhaseChanged += HandlePhaseChanged;
        }

        /// <summary>Shows the menu and its entries' effects for the flow's current phase.</summary>
        public void Show()
        {
            FrontEndPhase phase = flow.Phase;
            bool isVisible = phase == FrontEndPhase.MainMenu || phase == FrontEndPhase.FadingOut || phase == FrontEndPhase.OpeningCredits
                || phase == FrontEndPhase.ClosingCredits || phase == FrontEndPhase.Quitting || phase == FrontEndPhase.Quit;
            UiBinding.SetVisible(gameObject, isVisible);
            // nothing else can be clicked once an entry was chosen
            bool isInteractable = phase == FrontEndPhase.MainMenu;
            if (canvasGroup.interactable != isInteractable)
            {
                canvasGroup.interactable = isInteractable;
                canvasGroup.blocksRaycasts = isInteractable;
            }

            if (!isVisible)
            {
                return;
            }

            bool isLoading = phase == FrontEndPhase.FadingOut && flow.Choice == FrontEndChoice.LoadGame;
            loadGameCrumble.Progress = isLoading ? Mathf.InverseLerp(0f, crumbleEnd, flow.Progress) : 0f;
            ShowFall(isLoading ? Mathf.InverseLerp(fallStart, 1f, flow.Progress) : 0f);
            // the scroll rolls up again while the credits fade out
            creditsScroll.Show(phase == FrontEndPhase.OpeningCredits ? Mathf.InverseLerp(0f, unrollEnd, flow.Progress)
                : phase == FrontEndPhase.ClosingCredits ? 1f - flow.Progress
                : 0f);
            if (phase == FrontEndPhase.Quitting || phase == FrontEndPhase.Quit)
            {
                quitChopper.ShowDeparture(flow.Progress);
            }
            else
            {
                quitChopper.SetHovered(phase == FrontEndPhase.MainMenu && quit.IsHovered);
            }
        }

        private void OnDestroy()
        {
            if (newGameButton != null)
            {
                newGameButton.Hovered -= HandleNewGameHovered;
                newGameButton.Clicked -= HandleNewGameClicked;
            }

            if (loadGame != null)
            {
                loadGame.Clicked -= HandleLoadGameClicked;
            }

            if (credits != null)
            {
                credits.Clicked -= HandleCreditsClicked;
            }

            if (quit != null)
            {
                quit.Clicked -= HandleQuitClicked;
            }

            if (flow != null)
            {
                flow.PhaseChanged -= HandlePhaseChanged;
            }
        }

        // the camera falls towards the hole: it grows slowly at first and ever faster (exponentially, on a squared curve),
        // moves to the middle of the screen early and turns a little, until its black middle fills the screen
        private void ShowFall(float fall)
        {
            if (fall <= 0f)
            {
                if (isFalling)
                {
                    isFalling = false;
                    designFrame.localScale = Vector3.one;
                    designFrame.localRotation = Quaternion.identity;
                    designFrame.anchoredPosition = frameHome;
                }

                return;
            }

            if (!isFalling)
            {
                isFalling = true;
                designFrame.localScale = Vector3.one;
                designFrame.localRotation = Quaternion.identity;
                designFrame.anchoredPosition = frameHome;
            }

            // the hole's middle in the frame's own space, which its scale and turn do not change
            Vector2 hole = (Vector2)designFrame.InverseTransformPoint(loadGameHole.TransformPoint(loadGameHole.rect.center));
            float scale = Mathf.Pow(fallZoom, fall * fall);
            float centring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, centringEnd, fall));
            float angle = swirlDegrees * fall * fall;
            Vector2 turned = Quaternion.Euler(0f, 0f, angle) * (hole * scale);
            designFrame.localScale = new Vector3(scale, scale, 1f);
            designFrame.localRotation = Quaternion.Euler(0f, 0f, angle);
            designFrame.anchoredPosition = frameHome + hole * (1f - centring) - turned;
        }

        private void HandleNewGameHovered()
        {
            audioControls.Play(SoundCue.MainMenuHover);
        }

        private void HandleNewGameClicked()
        {
            if (flow.StartNewGame())
            {
                audioControls.Play(SoundCue.MainMenuNewGame);
            }
        }

        private void HandleLoadGameClicked()
        {
            if (flow.StartLoadGame())
            {
                audioControls.Play(SoundCue.Click);
            }
        }

        private void HandleCreditsClicked()
        {
            if (flow.OpenCredits())
            {
                audioControls.Play(SoundCue.Click);
            }
        }

        private void HandleQuitClicked()
        {
            if (flow.Quit())
            {
                audioControls.Play(SoundCue.Click);
            }
        }

        // back from the credits, the entries take clicks again
        private void HandlePhaseChanged(FrontEndPhase phase)
        {
            if (phase == FrontEndPhase.MainMenu)
            {
                loadGame.ResetClick();
                credits.ResetClick();
                quit.ResetClick();
                quitChopper.ResetAnimation();
            }
        }
    }
}
