using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The sealed scroll under the Credits title: its frames break the seal and unroll it downwards. It only draws the
    /// unroll progress it is given, so playing the progress backwards rolls it up again.
    /// </summary>
    public sealed class ScrollUnrollView : MonoBehaviour
    {
        [Tooltip("All frames share one canvas with the rolled scroll at its top; the rest of the canvas is the unrolled length.")]
        [SerializeField] private Image scroll;
        [Tooltip("Rolled and sealed to fully open.")]
        [SerializeField] private Sprite[] frames = Array.Empty<Sprite>();

        public Image Scroll => scroll;
        public int FrameCount => frames.Length;
        public Sprite CurrentFrame => scroll.sprite;

        /// <exception cref="InvalidOperationException">A reference or a frame is missing on the prefab.</exception>
        public void Initialize()
        {
            if (scroll == null || frames.Length < 2 || Array.Exists(frames, frame => frame == null))
            {
                throw new InvalidOperationException($"ScrollUnrollView on '{name}' needs its image and at least two frames. Assign them in the FrontEnd prefab.");
            }

            Show(0f);
        }

        /// <param name="progress">0 rolled and sealed, 1 fully open.</param>
        public void Show(float progress)
        {
            Sprite frame = frames[Mathf.RoundToInt(Mathf.Clamp01(progress) * (frames.Length - 1))];
            if (scroll.sprite != frame)
            {
                scroll.sprite = frame;
            }
        }
    }
}
