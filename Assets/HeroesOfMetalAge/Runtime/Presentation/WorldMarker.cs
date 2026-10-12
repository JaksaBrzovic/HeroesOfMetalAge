using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// A figure on the map or battlefield: a coloured base, an icon and an optional stack count. It animates itself
    /// (glide, lunge, hit flash, fade-out) from requests made by the views; it never decides anything about the game.
    /// </summary>
    public sealed class WorldMarker : MonoBehaviour
    {
        private const float LUNGE_SECONDS = 0.24f;
        private const float FLASH_SECONDS = 0.3f;
        private const float FADE_SECONDS = 0.45f;
        private const float ARRIVED_DISTANCE = 0.001f;

        [SerializeField] private SpriteRenderer baseRenderer;
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private TextMeshPro countText;
        [Tooltip("Largest side of the icon, in marker units (1 = one cell).")]
        [Min(0.1f)]
        [SerializeField] private float iconSize = 0.72f;

        private Vector3 position;
        private Vector3 target;
        private Vector3 lungeOffset;
        private Color iconTint = Color.white;
        private Color baseTint = Color.white;
        private float speed;
        private float lungeTime;
        private float flashTime;
        private float fadeTime;
        private bool isFading;

        public Vector3 Target => target;
        public bool IsMoving => (position - target).sqrMagnitude > ARRIVED_DISTANCE * ARRIVED_DISTANCE;

        /// <summary>True when the icon is actually drawn: active, with a sprite and not faded out.</summary>
        public bool HasVisibleIcon => gameObject.activeInHierarchy && iconRenderer.enabled && iconRenderer.sprite != null && iconRenderer.color.a > 0.01f;

        public void Display(Sprite icon, Color tint, Color baseColor, string count)
        {
            if (iconRenderer.sprite != icon)
            {
                iconRenderer.sprite = icon;
                FitIcon();
            }

            iconTint = tint;
            baseTint = baseColor;
            if (countText.text != count)
            {
                countText.text = count;
            }

            countText.gameObject.SetActive(!string.IsNullOrEmpty(count));
            ApplyColors();
        }

        public void SetFlipped(bool isFlipped)
        {
            iconRenderer.flipX = isFlipped;
        }

        /// <summary>Jumps to a position without animating.</summary>
        public void Place(Vector3 localPosition)
        {
            position = localPosition;
            target = localPosition;
            transform.localPosition = localPosition;
        }

        public void MoveTo(Vector3 localPosition, float unitsPerSecond)
        {
            target = localPosition;
            speed = unitsPerSecond;
        }

        /// <summary>A short strike towards <paramref name="offset"/> and back.</summary>
        public void Lunge(Vector3 offset)
        {
            lungeOffset = offset;
            lungeTime = LUNGE_SECONDS;
        }

        public void Flash()
        {
            flashTime = FLASH_SECONDS;
        }

        public void Show()
        {
            isFading = false;
            fadeTime = 0f;
            gameObject.SetActive(true);
            ApplyColors();
        }

        /// <summary>Fades out and then deactivates; repeated calls do not restart the fade.</summary>
        public void FadeOut()
        {
            if (isFading || !gameObject.activeSelf)
            {
                return;
            }

            isFading = true;
            fadeTime = FADE_SECONDS;
        }

        public void Tick(float deltaTime)
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            position = Vector3.MoveTowards(position, target, speed * deltaTime);
            Vector3 offset = Vector3.zero;
            if (lungeTime > 0f)
            {
                lungeTime = Mathf.Max(0f, lungeTime - deltaTime);
                offset = lungeOffset * Mathf.Sin(Mathf.PI * (1f - lungeTime / LUNGE_SECONDS));
            }

            transform.localPosition = position + offset;
            if (flashTime > 0f)
            {
                flashTime = Mathf.Max(0f, flashTime - deltaTime);
            }

            if (isFading)
            {
                fadeTime = Mathf.Max(0f, fadeTime - deltaTime);
                if (fadeTime <= 0f)
                {
                    isFading = false;
                    gameObject.SetActive(false);
                    return;
                }
            }

            ApplyColors();
        }

        private void ApplyColors()
        {
            float flash = flashTime / FLASH_SECONDS;
            float alpha = isFading ? fadeTime / FADE_SECONDS : 1f;
            iconRenderer.color = WithAlpha(Color.Lerp(iconTint, Color.white, flash), iconTint.a * alpha);
            baseRenderer.color = WithAlpha(Color.Lerp(baseTint, Color.white, flash * 0.6f), baseTint.a * alpha);
            countText.alpha = alpha;
        }

        // authored portraits and placeholder icons have different sizes; each is scaled to the same footprint
        private void FitIcon()
        {
            if (iconRenderer.sprite == null)
            {
                return;
            }

            Vector2 size = iconRenderer.sprite.bounds.size;
            float largest = Mathf.Max(size.x, size.y);
            iconRenderer.transform.localScale = largest > 0f ? Vector3.one * (iconSize / largest) : Vector3.one;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
