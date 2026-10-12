using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Writes to uGUI controls only when the value changed, so refreshes do not dirty unchanged layout.</summary>
    public static class UiBinding
    {
        public static void SetText(TMP_Text field, string text)
        {
            string value = text ?? string.Empty;
            if (field.text != value)
            {
                field.SetText(value);
            }
        }

        /// <summary>Shows a sprite with a tint; a missing sprite hides the Image.</summary>
        public static void SetSprite(Image image, Sprite sprite, Color tint)
        {
            if (image.sprite != sprite)
            {
                image.sprite = sprite;
            }

            image.enabled = sprite != null;
            if (image.color != tint)
            {
                image.color = tint;
            }
        }

        /// <summary>Fills a bar Image from the left by stretching its right anchor to <paramref name="value"/> (0..1).</summary>
        public static void SetProgress(Image bar, float value)
        {
            RectTransform rect = bar.rectTransform;
            Vector2 anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            if (rect.anchorMax != anchorMax)
            {
                rect.anchorMax = anchorMax;
            }
        }

        public static void SetVisible(GameObject target, bool isVisible)
        {
            if (target.activeSelf != isVisible)
            {
                target.SetActive(isVisible);
            }
        }

        public static void SetButton(UiWidget button, string title, bool isInteractable)
        {
            button.SetTitle(title);
            button.SetInteractable(isInteractable);
        }
    }
}
