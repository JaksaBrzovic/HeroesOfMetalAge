using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Root of the FrontEnd prefab, drawn above the game UI and the dialogue: the main menu, the black fade and the intro.
    /// It advances the <see cref="FrontEndFlow"/> and shows its phases; the fade blocks clicks from New Game until the
    /// mission is fully visible. Once the mission is being played the whole front end is hidden. Its own Canvas keeps the
    /// per-frame fades out of the game UI's rebuilds.
    /// </summary>
    public sealed class FrontEndView : MonoBehaviour
    {
        [SerializeField] private MainMenuView mainMenu;
        [Tooltip("Black over the whole screen, letterbox included.")]
        [SerializeField] private CanvasGroup fade;
        [Tooltip("Placeholder intro: the end-of-day hourglass.")]
        [SerializeField] private EndDayView intro;

        private FrontEndFlow flow;

        public MainMenuView MainMenu => mainMenu;
        public CanvasGroup Fade => fade;
        public EndDayView Intro => intro;

        /// <exception cref="InvalidOperationException">Called twice, or a reference is missing on the FrontEnd prefab.</exception>
        public void Initialize(FrontEndFlow frontEndFlow, IAudioControls audio)
        {
            if (flow != null)
            {
                throw new InvalidOperationException("Front end was initialized twice.");
            }

            if (mainMenu == null || fade == null || intro == null)
            {
                throw new InvalidOperationException($"FrontEndView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            flow = frontEndFlow;
            mainMenu.Initialize(flow, audio);
            intro.Initialize();
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

            float blackness = flow.Blackness;
            UiBinding.SetVisible(fade.gameObject, phase != FrontEndPhase.MainMenu);
            if (fade.alpha != blackness)
            {
                fade.alpha = blackness;
            }

            // the transition swallows every click, also on the fading-in mission below
            fade.blocksRaycasts = phase != FrontEndPhase.MainMenu;

            if (phase == FrontEndPhase.Intro)
            {
                intro.Show(flow.Progress);
            }
            else
            {
                intro.Hide();
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
