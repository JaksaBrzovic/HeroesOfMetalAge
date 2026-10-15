using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The main menu over the world-themed background: New Game starts the transition into the mission; Load Game, Credits
    /// and Quit are placeholders for now that only highlight and click.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private NewGameButtonView newGameButton;
        [Header("Placeholders")]
        [SerializeField] private UiWidget loadGameButton;
        [SerializeField] private UiWidget creditsButton;
        [SerializeField] private UiWidget quitButton;

        private FrontEndFlow flow;
        private IAudioControls audioControls;

        public NewGameButtonView NewGameButton => newGameButton;
        public UiWidget LoadGameButton => loadGameButton;
        public UiWidget CreditsButton => creditsButton;
        public UiWidget QuitButton => quitButton;

        /// <exception cref="InvalidOperationException">Called twice, or a reference is missing on the FrontEnd prefab.</exception>
        public void Initialize(FrontEndFlow frontEndFlow, IAudioControls audio)
        {
            if (flow != null)
            {
                throw new InvalidOperationException("Main menu was initialized twice.");
            }

            if (canvasGroup == null || newGameButton == null || loadGameButton == null || creditsButton == null || quitButton == null)
            {
                throw new InvalidOperationException($"MainMenuView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            flow = frontEndFlow;
            audioControls = audio;
            newGameButton.Initialize();
            newGameButton.Hovered += HandleNewGameHovered;
            newGameButton.Clicked += HandleNewGameClicked;
            foreach (UiWidget placeholder in new[] { loadGameButton, creditsButton, quitButton })
            {
                placeholder.Bind(PlayPlaceholderClick);
            }
        }

        /// <param name="isInteractable">False from New Game on, so nothing else can be clicked during the fade.</param>
        public void Display(bool isVisible, bool isInteractable)
        {
            UiBinding.SetVisible(gameObject, isVisible);
            if (canvasGroup.interactable != isInteractable)
            {
                canvasGroup.interactable = isInteractable;
                canvasGroup.blocksRaycasts = isInteractable;
            }
        }

        private void OnDestroy()
        {
            if (newGameButton != null)
            {
                newGameButton.Hovered -= HandleNewGameHovered;
                newGameButton.Clicked -= HandleNewGameClicked;
            }
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

        private void PlayPlaceholderClick()
        {
            audioControls.Play(SoundCue.Click);
        }
    }
}
