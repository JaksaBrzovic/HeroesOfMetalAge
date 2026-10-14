using System;
using UnityEngine;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>
    /// The comic-strip presentation: small bubbles rise from the speaker to a cloud balloon that holds the line; no
    /// portrait or name, the speaker is whoever the trail starts from. The root fills the design frame and draws nothing;
    /// only the cloud is a raycast target. The bubbles and the cloud pop in one after another when the balloon opens.
    /// </summary>
    public sealed class DialogueBalloonView : DialogueView
    {
        // overshoot of the pop, as in an ease-out-back curve
        private const float POP_OVERSHOOT = 1.7f;

        [Tooltip("The cloud that holds the text.")]
        [SerializeField] private RectTransform balloon;
        [Tooltip("Bubbles from the speaker to the cloud, the one nearest the speaker first.")]
        [SerializeField] private RectTransform[] trailBubbles = Array.Empty<RectTransform>();
        [Tooltip("Cloud centre relative to the speaker, in design pixels; mirrored when the cloud would leave the screen.")]
        [SerializeField] private Vector2 balloonOffset = new Vector2(260f, 260f);
        [Tooltip("Speaker point used when no position is given, (0,0) bottom-left to (1,1) top-right.")]
        [SerializeField] private Vector2 defaultSpeakerPosition = new Vector2(0.3f, 0.35f);
        [Tooltip("Seconds each bubble and the cloud take to pop in; the next starts when the previous is done.")]
        [Min(0.01f)]
        [SerializeField] private float popSeconds = 0.07f;

        private float entranceSeconds;

        public override bool IsEntranceComplete => entranceSeconds >= EntranceDuration;
        /// <summary>The cloud's position in the design frame, for tests and tooling.</summary>
        public Vector2 BalloonPosition => balloon.anchoredPosition;

        private float EntranceDuration => (trailBubbles.Length + 1) * popSeconds;

        public override void Initialize()
        {
            base.Initialize();
            Require(balloon, nameof(balloon));
            for (int i = 0; i < trailBubbles.Length; i++)
            {
                Require(trailBubbles[i], nameof(trailBubbles) + "[" + i + "]");
            }
        }

        public override void Open()
        {
            base.Open();
            entranceSeconds = 0f;
            ApplyEntrance();
        }

        public override void PlaceAuthored()
        {
            PlaceAt(defaultSpeakerPosition);
        }

        /// <summary>
        /// Starts the trail at the speaker's point and puts the cloud beside it, mirrored and clamped so that the cloud stays
        /// inside the frame; the bubbles are spaced evenly from the speaker to the cloud's edge.
        /// </summary>
        public override void PlaceAt(Vector2 normalizedPosition)
        {
            Rect area = Rect.rect;
            Vector2 speaker = new Vector2(Mathf.Lerp(area.xMin, area.xMax, Mathf.Clamp01(normalizedPosition.x)),
                Mathf.Lerp(area.yMin, area.yMax, Mathf.Clamp01(normalizedPosition.y)));
            Vector2 half = balloon.rect.size * 0.5f;
            Vector2 offset = balloonOffset;
            if (speaker.x + offset.x + half.x > area.xMax)
            {
                offset.x = -offset.x;
            }

            if (speaker.y + offset.y + half.y > area.yMax)
            {
                offset.y = -offset.y;
            }

            Vector2 centre = speaker + offset;
            centre.x = Mathf.Clamp(centre.x, area.xMin + half.x, area.xMax - half.x);
            centre.y = Mathf.Clamp(centre.y, area.yMin + half.y, area.yMax - half.y);
            balloon.anchoredPosition = centre - area.center;

            Vector2 edge = EdgeTowards(centre, half, speaker);
            for (int i = 0; i < trailBubbles.Length; i++)
            {
                float t = (i + 1f) / (trailBubbles.Length + 1f);
                trailBubbles[i].anchoredPosition = Vector2.Lerp(speaker, edge, t) - area.center;
            }
        }

        public override void AdvanceEntrance(float deltaSeconds)
        {
            if (!IsEntranceComplete)
            {
                entranceSeconds += Mathf.Max(0f, deltaSeconds);
                ApplyEntrance();
            }
        }

        public override void CompleteEntrance()
        {
            entranceSeconds = EntranceDuration;
            ApplyEntrance();
        }

        protected override void ShowSpeaker(DialogueSpeakerEntry speaker)
        {
            // a balloon shows only the words; the trail points at the speaker
        }

        // bubbles pop first, nearest the speaker first, then the cloud
        private void ApplyEntrance()
        {
            for (int i = 0; i < trailBubbles.Length; i++)
            {
                SetScale(trailBubbles[i], PopScale(entranceSeconds - i * popSeconds));
            }

            SetScale(balloon, PopScale(entranceSeconds - trailBubbles.Length * popSeconds));
        }

        private float PopScale(float elapsedSeconds)
        {
            float t = Mathf.Clamp01(elapsedSeconds / popSeconds);
            if (t >= 1f)
            {
                return 1f;
            }

            float u = t - 1f;
            return 1f + u * u * ((POP_OVERSHOOT + 1f) * u + POP_OVERSHOOT);
        }

        private static void SetScale(RectTransform target, float scale)
        {
            Vector3 value = new Vector3(scale, scale, 1f);
            if (target.localScale != value)
            {
                target.localScale = value;
            }
        }

        // the point of the cloud's ellipse in the speaker's direction, slightly inside so the last bubble touches it
        private static Vector2 EdgeTowards(Vector2 centre, Vector2 half, Vector2 speaker)
        {
            Vector2 direction = speaker - centre;
            if (direction.sqrMagnitude < 0.01f)
            {
                return centre;
            }

            direction.Normalize();
            float distance = 1f / Mathf.Sqrt(direction.x * direction.x / (half.x * half.x) + direction.y * direction.y / (half.y * half.y));
            return centre + direction * distance * 0.85f;
        }
    }
}
