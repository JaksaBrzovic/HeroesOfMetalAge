using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Root of the FrontEnd prefab, drawn above the game UI and the dialogue: the main menu, the credits, the black fade
    /// and the intro. It advances the <see cref="FrontEndFlow"/> and shows its phases; the fade and then the intro's curtain
    /// block clicks from New Game or Load Game until the curtain has burnt away. Once the mission is being played the whole front end is hidden. Its own Canvas keeps the
    /// per-frame fades out of the game UI's rebuilds.
    /// </summary>
    public sealed class FrontEndView : MonoBehaviour
    {
        [SerializeField] private MainMenuView mainMenu;
        [SerializeField] private CreditsView credits;
        [Tooltip("Black over the whole screen, letterbox included.")]
        [SerializeField] private CanvasGroup fade;
        [Tooltip("The verses, the title and the burning curtain.")]
        [SerializeField] private IntroView introView;
        [Tooltip("Share of Load Game's way to black after which the black closes in, as the camera falls into the hole.")]
        [Range(0f, 0.99f)]
        [SerializeField] private float fallBlackStart = 0.8f;
        [Tooltip("Share of the credits' opening after which the credits screen starts to fade in.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float creditsFadeStart = 0.5f;

        private FrontEndFlow flow;
        private IAudioControls audioControls;

        public MainMenuView MainMenu => mainMenu;
        public CreditsView Credits => credits;
        public CanvasGroup Fade => fade;
        public IntroView Intro => introView;

        /// <param name="creditsText">The rolling credits (rich text).</param>
        /// <exception cref="InvalidOperationException">Called twice, or a reference is missing on the FrontEnd prefab.</exception>
        public void Initialize(FrontEndFlow frontEndFlow, IAudioControls audio, string creditsText)
        {
            if (flow != null)
            {
                throw new InvalidOperationException("Front end was initialized twice.");
            }

            if (mainMenu == null || credits == null || fade == null || introView == null)
            {
                throw new InvalidOperationException($"FrontEndView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            flow = frontEndFlow;
            audioControls = audio;
            mainMenu.Initialize(flow, audio);
            credits.Initialize(creditsText, HandleCreditsBack);
            introView.Initialize(flow.Intro);
            Refresh();
        }

        public void Refresh()
        {
            FrontEndPhase phase = flow.Phase;
            if (phase == FrontEndPhase.Playing)
            {
                UiBinding.SetVisible(gameObject, false);
                return;
            }

            UiBinding.SetVisible(gameObject, true);
            mainMenu.Show();
            ShowCredits(phase);

            // New Game fades to black; Load Game turns black only at the end of the camera's fall into the hole
            bool isLoading = flow.Choice == FrontEndChoice.LoadGame;
            UiBinding.SetVisible(fade.gameObject, phase == FrontEndPhase.FadingOut || phase == FrontEndPhase.Black);
            float blackness = phase == FrontEndPhase.FadingOut && isLoading
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fallBlackStart, 1f, flow.Progress))
                : flow.Blackness;
            if (fade.alpha != blackness)
            {
                fade.alpha = blackness;
            }

            if (phase == FrontEndPhase.Verses || phase == FrontEndPhase.Title || phase == FrontEndPhase.Burning)
            {
                introView.Show(flow);
            }
            else
            {
                introView.Hide();
            }
        }

        private void Update()
        {
            if (flow == null)
            {
                return;
            }

            flow.Tick(Time.unscaledDeltaTime);
            Refresh();
        }

        private void ShowCredits(FrontEndPhase phase)
        {
            switch (phase)
            {
                case FrontEndPhase.OpeningCredits:
                    credits.Show(Mathf.InverseLerp(creditsFadeStart, 1f, flow.Progress), 0f, false);
                    break;
                case FrontEndPhase.Credits:
                    credits.Show(1f, flow.Elapsed, true);
                    break;
                case FrontEndPhase.ClosingCredits:
                    credits.Show(1f - flow.Progress, 0f, false);
                    break;
                default:
                    credits.Hide();
                    break;
            }
        }

        private void HandleCreditsBack()
        {
            if (flow.CloseCredits())
            {
                audioControls.Play(SoundCue.Click);
            }
        }
    }
}
