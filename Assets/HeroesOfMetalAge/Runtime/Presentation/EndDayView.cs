using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The hourglass shown in the middle of the screen while the day ends: it fades in, plays its painted animation (the sand
    /// runs out, it holds, turns over and settles) over the whole sequence and fades out. It only displays the session's
    /// end-of-day progress; the session decides when the day ends. Its own Canvas keeps the per-frame animation out of the
    /// game UI's rebuilds.
    /// </summary>
    public sealed class EndDayView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Shows the current frame.")]
        [SerializeField] private Image hourglass;
        [Tooltip("The animation's frames in order.")]
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();
        [Tooltip("Relative length of each frame (any unit, e.g. the authored milliseconds); the frames fill the whole sequence. Empty plays them evenly.")]
        [SerializeField] private float[] frameWeights = Array.Empty<float>();

        [Header("Timing (fractions of the sequence)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float fadeInEnd = 0.06f;
        [Range(0f, 1f)]
        [SerializeField] private float fadeOutStart = 0.92f;

        /// <summary>The frame on screen now.</summary>
        public Sprite CurrentFrame => hourglass.sprite;
        public int FrameCount => frames.Length;

        /// <exception cref="InvalidOperationException">A reference or a frame is missing on the EndDayOverlay prefab.</exception>
        public void Initialize()
        {
            if (canvasGroup == null || hourglass == null || frames.Length == 0 || Array.Exists(frames, frame => frame == null))
            {
                throw new InvalidOperationException($"EndDayView on '{name}' needs its canvas group, hourglass image and frames. Assign them in the EndDayOverlay prefab.");
            }

            Hide();
        }

        /// <param name="progress">0 when the day starts ending, 1 when the new day begins.</param>
        public void Show(float progress)
        {
            progress = Mathf.Clamp01(progress);
            UiBinding.SetVisible(gameObject, true);

            float fadeIn = fadeInEnd > 0f ? progress / fadeInEnd : 1f;
            float fadeOut = 1f - Mathf.InverseLerp(fadeOutStart, 1f, progress);
            canvasGroup.alpha = Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));

            Sprite frame = frames[FrameAt(progress)];
            if (hourglass.sprite != frame)
            {
                hourglass.sprite = frame;
            }
        }

        public void Hide()
        {
            UiBinding.SetVisible(gameObject, false);
        }

        /// <summary>The frame shown at a progress of the sequence, by the frames' relative lengths.</summary>
        internal int FrameAt(float progress)
        {
            if (frameWeights.Length != frames.Length)
            {
                return Mathf.Min(frames.Length - 1, (int)(Mathf.Clamp01(progress) * frames.Length));
            }

            float total = 0f;
            foreach (float weight in frameWeights)
            {
                total += Mathf.Max(0f, weight);
            }

            float elapsed = Mathf.Clamp01(progress) * total;
            for (int i = 0; i < frameWeights.Length; i++)
            {
                elapsed -= Mathf.Max(0f, frameWeights[i]);
                if (elapsed < 0f)
                {
                    return i;
                }
            }

            return frames.Length - 1;
        }
    }
}
