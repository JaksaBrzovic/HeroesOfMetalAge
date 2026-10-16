using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Root of the FrontEnd prefab, drawn above the game UI and the dialogue: the main menu, the black fade and the intro.
    /// It advances the <see cref="FrontEndFlow"/> and shows its phases; the fade and then the intro's curtain block clicks
    /// from New Game until the curtain has burnt away. Once the mission is being played the whole front end is hidden. Its own Canvas keeps the
    /// per-frame fades out of the game UI's rebuilds.
    /// </summary>
    public sealed class FrontEndView : MonoBehaviour
    {
        [SerializeField] private MainMenuView mainMenu;
        [Tooltip("Black over the whole screen, letterbox included.")]
        [SerializeField] private CanvasGroup fade;
        [Tooltip("The verses, the title and the burning curtain.")]
        [SerializeField] private IntroView introView;

        private FrontEndFlow flow;

        public MainMenuView MainMenu => mainMenu;
        public CanvasGroup Fade => fade;
        public IntroView Intro => introView;

        /// <exception cref="InvalidOperationException">Called twice, or a reference is missing on the FrontEnd prefab.</exception>
        public void Initialize(FrontEndFlow frontEndFlow, IAudioControls audio)
        {
            if (flow != null)
            {
                throw new InvalidOperationException("Front end was initialized twice.");
            }

            if (mainMenu == null || fade == null || introView == null)
            {
                throw new InvalidOperationException($"FrontEndView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            flow = frontEndFlow;
            mainMenu.Initialize(flow, audio);
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
            mainMenu.Display(phase == FrontEndPhase.MainMenu || phase == FrontEndPhase.FadingOut, phase == FrontEndPhase.MainMenu);

            // the fade covers the menu until the intro's curtain takes over; both swallow every click
            float blackness = flow.Blackness;
            UiBinding.SetVisible(fade.gameObject, phase == FrontEndPhase.FadingOut || phase == FrontEndPhase.Black);
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
    }
}
