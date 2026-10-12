using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Binding surface of the authored widget prefabs (buttons, slots, cards, tiles, markers). Every reference is optional
    /// because each widget prefab uses a different subset; the hierarchy is never built at runtime.
    /// </summary>
    public sealed class UiWidget : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private Image iconImage;
        [SerializeField] private Image backgroundImage;

        private Action clickAction;

        public Button Button => button;
        public RectTransform Rect => (RectTransform)transform;

        /// <summary>True only if the icon Image would actually draw something on screen.</summary>
        public bool HasVisibleIcon => iconImage != null
            && iconImage.sprite != null
            && iconImage.enabled
            && iconImage.gameObject.activeInHierarchy
            && iconImage.color.a > 0f
            && iconImage.rectTransform.rect.width > 0f
            && iconImage.rectTransform.rect.height > 0f;

        /// <summary>Replaces the click action; one action per widget, so rebinding a reused widget never stacks listeners.</summary>
        public void Bind(Action action)
        {
            clickAction = action;
        }

        public void Unbind()
        {
            clickAction = null;
        }

        public void Display(string title, string description, string amount, Sprite icon, Color iconColor, bool isInteractable)
        {
            SetOptionalText(titleText, title);
            SetOptionalText(descriptionText, description);
            SetOptionalText(amountText, amount);
            SetIcon(icon, iconColor);
            SetInteractable(isInteractable);
        }

        public void SetTitle(string title)
        {
            SetOptionalText(titleText, title);
        }

        public void SetInteractable(bool isInteractable)
        {
            if (button != null && button.interactable != isInteractable)
            {
                button.interactable = isInteractable;
            }
        }

        public void SetBackground(Color color)
        {
            if (backgroundImage != null && backgroundImage.color != color)
            {
                backgroundImage.color = color;
            }
        }

        public void SetIcon(Sprite sprite, Color color)
        {
            if (iconImage != null)
            {
                UiBinding.SetSprite(iconImage, sprite, color);
            }
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void HandleClicked()
        {
            clickAction?.Invoke();
        }

        private static void SetOptionalText(TMP_Text field, string value)
        {
            if (field != null)
            {
                UiBinding.SetText(field, value);
            }
        }
    }
}
