using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The credits screen: the credits roll up from below the viewport and start again once they have gone past its top.
    /// A click anywhere on the screen asks to go back to the main menu. It only draws the alpha and the rolling time it is
    /// given; the front end's flow decides when it opens and closes.
    /// </summary>
    public sealed class CreditsView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Covers the whole screen; any click on it goes back.")]
        [SerializeField] private Button backButton;
        [Tooltip("Clips the rolling text (RectMask2D).")]
        [SerializeField] private RectTransform viewport;
        [Tooltip("Anchored to the viewport's top, pivot at its top; its height is set to fit the text.")]
        [SerializeField] private TMP_Text creditsText;
        [Tooltip("Canvas units per second.")]
        [Min(1f)]
        [SerializeField] private float scrollSpeed = 70f;

        private Action back;

        public TMP_Text CreditsText => creditsText;
        /// <summary>How far the text has rolled up from its start below the viewport.</summary>
        public float ScrollOffset { get; private set; }

        /// <exception cref="InvalidOperationException">A reference is missing on the prefab.</exception>
        public void Initialize(string credits, Action onBack)
        {
            if (canvasGroup == null || backButton == null || viewport == null || creditsText == null)
            {
                throw new InvalidOperationException($"CreditsView on '{name}' has unassigned references. Assign them in the FrontEnd prefab.");
            }

            back = onBack;
            backButton.onClick.RemoveListener(HandleBackClicked);
            backButton.onClick.AddListener(HandleBackClicked);
            UiBinding.SetText(creditsText, credits);
            // the text is set once, so its height is measured once
            float height = creditsText.GetPreferredValues(credits, viewport.rect.width, 0f).y;
            creditsText.rectTransform.sizeDelta = new Vector2(creditsText.rectTransform.sizeDelta.x, height);
            Hide();
        }

        /// <param name="rollingSeconds">Seconds the credits have rolled.</param>
        /// <param name="isInteractable">False while the screen opens and closes, so a click cannot go back twice.</param>
        public void Show(float alpha, float rollingSeconds, bool isInteractable)
        {
            UiBinding.SetVisible(gameObject, true);
            if (canvasGroup.alpha != alpha)
            {
                canvasGroup.alpha = alpha;
            }

            // the screen always swallows clicks, so the menu below never takes one
            if (canvasGroup.interactable != isInteractable)
            {
                canvasGroup.interactable = isInteractable;
            }

            float viewportHeight = viewport.rect.height;
            float travel = creditsText.rectTransform.rect.height + viewportHeight;
            ScrollOffset = travel > 0f ? Mathf.Repeat(rollingSeconds * scrollSpeed, travel) : 0f;
            Vector2 position = new Vector2(0f, ScrollOffset - viewportHeight);
            if (creditsText.rectTransform.anchoredPosition != position)
            {
                creditsText.rectTransform.anchoredPosition = position;
            }
        }

        public void Hide()
        {
            UiBinding.SetVisible(gameObject, false);
        }

        internal void ClickBack()
        {
            HandleBackClicked();
        }

        private void OnDestroy()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveListener(HandleBackClicked);
            }
        }

        private void HandleBackClicked()
        {
            back?.Invoke();
        }
    }
}
