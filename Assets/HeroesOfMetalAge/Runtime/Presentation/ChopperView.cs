using System;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The hooded rider on his chopper under the Quit title. Hovering Quit lifts his foot onto the peg (the hover frames
    /// forward, holding the last; leaving plays them backwards from wherever they are). On Quit he first finishes lifting
    /// his foot, then rides off into the distance (the departure frames, whose first repeats the last hover frame and is
    /// skipped, and whose last is empty). Hover frames advance in unscaled time; the departure follows the progress it is
    /// given, so it always ends with the quitting phase.
    /// </summary>
    public sealed class ChopperView : MonoBehaviour
    {
        [SerializeField] private Image chopper;
        [Tooltip("Resting rider to foot on the peg; the first frame is the rider at rest.")]
        [SerializeField] private Sprite[] hoverFrames = Array.Empty<Sprite>();
        [Tooltip("Foot on the peg to gone; the first frame repeats the last hover frame, the last is empty.")]
        [SerializeField] private Sprite[] departureFrames = Array.Empty<Sprite>();
        [Min(1f)]
        [SerializeField] private float hoverFramesPerSecond = 12f;

        private int hoverIndex;
        private float frameTimer;
        private bool isHovered;
        // the hover frame the departure started from, or -1 while he stays
        private int departureStart = -1;
        private Sprite current;

        public Image Chopper => chopper;
        public Sprite CurrentFrame => current;
        public int HoverFrameCount => hoverFrames.Length;
        public int DepartureFrameCount => departureFrames.Length;

        /// <exception cref="InvalidOperationException">A reference or a frame is missing on the prefab.</exception>
        public void Initialize()
        {
            if (chopper == null || hoverFrames.Length == 0 || departureFrames.Length < 2 || Array.Exists(hoverFrames, frame => frame == null)
                || Array.Exists(departureFrames, frame => frame == null))
            {
                throw new InvalidOperationException($"ChopperView on '{name}' needs its image, hover frames and at least two departure frames.");
            }

            ResetAnimation();
        }

        /// <summary>Back to the rider at rest.</summary>
        public void ResetAnimation()
        {
            hoverIndex = 0;
            frameTimer = 0f;
            isHovered = false;
            departureStart = -1;
            Show(hoverFrames[0]);
        }

        /// <summary>The pointer is on the Quit entry (true) or not; ignored once he leaves.</summary>
        public void SetHovered(bool hovered)
        {
            isHovered = hovered;
        }

        /// <param name="progress">0 as Quit is chosen, 1 when he is gone.</param>
        public void ShowDeparture(float progress)
        {
            if (departureStart < 0)
            {
                departureStart = hoverIndex;
            }

            // the rest of the hover, then the departure without its first frame
            int hoverLeft = hoverFrames.Length - 1 - departureStart;
            int count = hoverLeft + departureFrames.Length - 1;
            int step = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * count), 0, count - 1);
            if (Mathf.Clamp01(progress) >= 1f)
            {
                step = count - 1;
            }

            Show(step < hoverLeft ? hoverFrames[departureStart + 1 + step] : departureFrames[step - hoverLeft + 1]);
        }

        internal void Tick(float deltaSeconds)
        {
            if (departureStart >= 0)
            {
                return;
            }

            frameTimer += Mathf.Max(0f, deltaSeconds);
            float frameSeconds = 1f / hoverFramesPerSecond;
            while (frameTimer >= frameSeconds)
            {
                frameTimer -= frameSeconds;
                if (isHovered && hoverIndex < hoverFrames.Length - 1)
                {
                    hoverIndex++;
                }
                else if (!isHovered && hoverIndex > 0)
                {
                    hoverIndex--;
                }
            }

            Show(hoverFrames[hoverIndex]);
        }

        private void Update()
        {
            if (hoverFrames.Length > 0)
            {
                Tick(Time.unscaledDeltaTime);
            }
        }

        private void Show(Sprite frame)
        {
            current = frame;
            if (chopper.sprite != frame)
            {
                chopper.sprite = frame;
            }
        }
    }
}
