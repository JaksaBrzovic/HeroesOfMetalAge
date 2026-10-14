using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>
    /// The portrait dialogue box: the speaker's portrait on the left, name and line on the right. Its background must be a
    /// raycast target, since clicks on it advance the dialogue.
    /// </summary>
    public sealed class DialogueBoxView : DialogueView
    {
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text speakerNameText;
        [Tooltip("Shown when a speaker has no portrait.")]
        [SerializeField] private Sprite fallbackPortrait;

        private Vector2 authoredAnchorMin;
        private Vector2 authoredAnchorMax;
        private Vector2 authoredPivot;
        private Vector2 authoredPosition;

        public override void Initialize()
        {
            base.Initialize();
            Require(portraitImage, nameof(portraitImage));
            Require(speakerNameText, nameof(speakerNameText));
            RectTransform rect = Rect;
            authoredAnchorMin = rect.anchorMin;
            authoredAnchorMax = rect.anchorMax;
            authoredPivot = rect.pivot;
            authoredPosition = rect.anchoredPosition;
        }

        public override void PlaceAuthored()
        {
            Place(authoredAnchorMin, authoredAnchorMax, authoredPivot, authoredPosition);
        }

        /// <summary>
        /// The box's pivot moves to the same point as its anchor, so it stays fully inside its parent at any position:
        /// centred at (0.5,0.5), in the corner at (1,1).
        /// </summary>
        public override void PlaceAt(Vector2 normalizedPosition)
        {
            Vector2 point = new Vector2(Mathf.Clamp01(normalizedPosition.x), Mathf.Clamp01(normalizedPosition.y));
            Place(point, point, point, Vector2.zero);
        }

        protected override void ShowSpeaker(DialogueSpeakerEntry speaker)
        {
            portraitImage.sprite = speaker.Portrait != null ? speaker.Portrait : fallbackPortrait;
            portraitImage.enabled = portraitImage.sprite != null;
            speakerNameText.SetText(speaker.DisplayName ?? string.Empty);
        }

        private void Place(Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position)
        {
            RectTransform rect = Rect;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
        }
    }
}
