using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// A figure standing on a map cell or hex: a ground shadow in its side's colour, the figure and an optional stack count.
    /// It animates itself (walk cycle and facing while it moves, lunge, hit flash, fade-out) from requests made by the views;
    /// it never decides anything about the game. Art of any size and pivot is scaled to the same height with its feet on the
    /// ground line.
    /// </summary>
    public sealed class WorldMarker : MonoBehaviour
    {
        private const float LUNGE_SECONDS = 0.24f;
        private const float FLASH_SECONDS = 0.3f;
        private const float FADE_SECONDS = 0.45f;
        private const float ARRIVED_DISTANCE = 0.001f;
        private const float PULSE_SPEED = 5f;
        private const float PULSE_SCALE = 0.08f;
        private const float FACING_THRESHOLD = 0.001f;

        [SerializeField] private SpriteRenderer baseRenderer;
        [SerializeField] private SpriteRenderer iconRenderer;
        [SerializeField] private TextMeshPro countText;
        [Tooltip("Ring shown around the figure whose turn it is.")]
        [SerializeField] private SpriteRenderer selectionRenderer;
        [Tooltip("Height of the figure, in marker units (1 = one cell).")]
        [Min(0.1f)]
        [SerializeField] private float iconHeight = 1.1f;
        [Tooltip("Widest the figure may be, in marker units.")]
        [Min(0.1f)]
        [SerializeField] private float maxIconWidth = 1.3f;
        [Tooltip("Where the feet stand, in marker units from the cell centre.")]
        [SerializeField] private float feetHeight = -0.36f;

        private Vector3 position;
        private Vector3 target;
        private Vector3 lungeOffset;
        private Color iconTint = Color.white;
        private Color baseTint = Color.white;
        private float speed;
        private float lungeTime;
        private float flashTime;
        private float fadeTime;
        private float pulseTime;
        private Vector3 selectionScale = Vector3.one;
        private IReadOnlyList<Sprite> frames = Array.Empty<Sprite>();
        private Sprite idleIcon;
        private float framesPerSecond;
        private float walkTime;
        private bool isFacingLeft;
        private bool defaultFacingLeft;
        private bool keepsMotionFacing;
        private bool hasFacing;
        private bool isFading;

        public Vector3 Target => target;
        public bool IsSelected => selectionRenderer.gameObject.activeSelf;
        public bool IsMoving => (position - target).sqrMagnitude > ARRIVED_DISTANCE * ARRIVED_DISTANCE;

        /// <summary>True when the icon is actually drawn: active, with a sprite and not faded out.</summary>
        public bool HasVisibleIcon => gameObject.activeInHierarchy && iconRenderer.enabled && iconRenderer.sprite != null && iconRenderer.color.a > 0.01f;

        /// <summary>Shows <paramref name="icon"/> while standing; a walk cycle set with <see cref="SetFrames"/> plays while moving.</summary>
        public void Display(Sprite icon, Color tint, Color baseColor, string count)
        {
            idleIcon = icon;
            if (!IsMoving || frames.Count == 0)
            {
                ShowSprite(icon);
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

        /// <summary>A right-facing walk cycle played while the figure moves; empty for still art.</summary>
        public void SetFrames(IReadOnlyList<Sprite> walkFrames, float walkFramesPerSecond)
        {
            frames = walkFrames ?? Array.Empty<Sprite>();
            framesPerSecond = walkFramesPerSecond;
        }

        /// <summary>
        /// Art faces right; <paramref name="faceLeft"/> mirrors it. While moving the figure faces where it goes; afterwards it
        /// keeps that facing if <paramref name="keepMotionFacing"/>, else returns to the given one.
        /// </summary>
        public void SetFacing(bool faceLeft, bool keepMotionFacing)
        {
            defaultFacingLeft = faceLeft;
            keepsMotionFacing = keepMotionFacing;
            if (!IsMoving && (!keepMotionFacing || !hasFacing))
            {
                isFacingLeft = faceLeft;
                iconRenderer.flipX = faceLeft;
            }

            hasFacing = true;
        }

        /// <summary>Shows a pulsing ring in <paramref name="color"/> while this figure is the one acting.</summary>
        public void SetSelected(bool isSelected, Color color)
        {
            if (isSelected && !IsSelected)
            {
                selectionScale = selectionRenderer.transform.localScale;
                pulseTime = 0f;
            }
            else if (!isSelected && IsSelected)
            {
                selectionRenderer.transform.localScale = selectionScale;
            }

            selectionRenderer.color = color;
            selectionRenderer.gameObject.SetActive(isSelected);
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

            float stepX = target.x - position.x;
            position = Vector3.MoveTowards(position, target, speed * deltaTime);
            Animate(stepX, deltaTime);
            Vector3 offset = Vector3.zero;
            if (lungeTime > 0f)
            {
                lungeTime = Mathf.Max(0f, lungeTime - deltaTime);
                offset = lungeOffset * Mathf.Sin(Mathf.PI * (1f - lungeTime / LUNGE_SECONDS));
            }

            transform.localPosition = position + offset;
            if (IsSelected)
            {
                pulseTime += deltaTime;
                selectionRenderer.transform.localScale = selectionScale * (1f + PULSE_SCALE * Mathf.Sin(pulseTime * PULSE_SPEED));
            }

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
        // the walk cycle plays only while moving; the figure turns to where it goes and, unless told to keep that, turns back
        private void Animate(float stepX, float deltaTime)
        {
            if (IsMoving)
            {
                if (Mathf.Abs(stepX) > FACING_THRESHOLD)
                {
                    isFacingLeft = stepX < 0f;
                }

                if (frames.Count > 0)
                {
                    walkTime += deltaTime;
                    ShowSprite(frames[(int)(walkTime * framesPerSecond) % frames.Count]);
                }
            }
            else
            {
                walkTime = 0f;
                if (!keepsMotionFacing)
                {
                    isFacingLeft = defaultFacingLeft;
                }

                ShowSprite(idleIcon);
            }

            iconRenderer.flipX = isFacingLeft;
        }

        private void ShowSprite(Sprite sprite)
        {
            if (iconRenderer.sprite == sprite)
            {
                return;
            }

            iconRenderer.sprite = sprite;
            FitIcon();
        }

        // art of any size and pivot is scaled to one height (within a maximum width) and stands with its feet on the ground line
        private void FitIcon()
        {
            Sprite sprite = iconRenderer.sprite;
            if (sprite == null)
            {
                return;
            }

            Bounds bounds = sprite.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
            {
                return;
            }

            float scale = Mathf.Min(iconHeight / bounds.size.y, maxIconWidth / bounds.size.x);
            Transform icon = iconRenderer.transform;
            icon.localScale = Vector3.one * scale;
            icon.localPosition = new Vector3(-bounds.center.x * scale, feetHeight - bounds.min.y * scale, 0f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
