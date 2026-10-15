using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The hourglass shown in the middle of the screen while the day ends: it fades in, its sand runs from the top bulb into
    /// the bottom one, it turns over and fades out. It only displays the session's end-of-day progress; the session decides
    /// when the day ends. Its own Canvas keeps the per-frame sand animation out of the game UI's rebuilds.
    /// </summary>
    public sealed class EndDayView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Turned over at the end of the sequence.")]
        [SerializeField] private RectTransform hourglass;
        [Tooltip("Filled image (vertical, from the bottom): the sand surface sinks towards the neck.")]
        [SerializeField] private Image topSand;
        [Tooltip("Filled image (vertical, from the bottom): the pile grows.")]
        [SerializeField] private Image bottomSand;
        [Tooltip("The thin stream from the neck, visible while sand runs.")]
        [SerializeField] private Image sandStream;

        [Header("Timing (fractions of the sequence)")]
        [Range(0f, 0.5f)]
        [SerializeField] private float fadeInEnd = 0.08f;
        [Tooltip("The sand has run out here and the hourglass starts to turn over.")]
        [Range(0f, 1f)]
        [SerializeField] private float turnStart = 0.78f;
        [Range(0f, 1f)]
        [SerializeField] private float turnEnd = 0.92f;
        [Range(0f, 1f)]
        [SerializeField] private float fadeOutStart = 0.9f;

        /// <exception cref="InvalidOperationException">A reference is missing on the EndDayOverlay prefab.</exception>
        public void Initialize()
        {
            if (canvasGroup == null || hourglass == null || topSand == null || bottomSand == null || sandStream == null)
            {
                throw new InvalidOperationException($"EndDayView on '{name}' has unassigned references. Assign them in the EndDayOverlay prefab.");
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

            float run = Mathf.InverseLerp(0f, turnStart, progress);
            topSand.fillAmount = 1f - run;
            bottomSand.fillAmount = run;
            sandStream.enabled = run > 0f && run < 1f;

            float turn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(turnStart, turnEnd, progress));
            hourglass.localEulerAngles = new Vector3(0f, 0f, 180f * turn);
        }

        public void Hide()
        {
            UiBinding.SetVisible(gameObject, false);
        }
    }
}
