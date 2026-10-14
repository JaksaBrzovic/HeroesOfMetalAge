using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>
    /// Base of the dialogue presentations (portrait box, speech balloon). A view displays what the director tells it and
    /// reports clicks on itself; it never decides what comes next. Clicks on child graphics reach it because the click
    /// handler sits on the view's root.
    /// </summary>
    public abstract class DialogueView : MonoBehaviour, IPointerClickHandler
    {
        private const string NEXT_LABEL = "NEXT";
        private const string CLOSE_LABEL = "CLOSE";

        [SerializeField] private TMP_Text lineText;
        [Tooltip("Shown once the whole line is visible.")]
        [SerializeField] private TMP_Text continueText;

        public event Action Clicked;

        public RectTransform Rect => (RectTransform)transform;
        public bool IsVisible => gameObject.activeSelf;
        public int VisibleCharacters => lineText.maxVisibleCharacters;
        public bool IsContinueVisible => continueText.gameObject.activeSelf;
        /// <summary>False while the view is still appearing; lines type only once it has appeared.</summary>
        public virtual bool IsEntranceComplete => true;

        /// <summary>Checks the serialized references and remembers the authored layout.</summary>
        /// <exception cref="InvalidOperationException">A serialized reference is missing on the prefab.</exception>
        public virtual void Initialize()
        {
            Require(lineText, nameof(lineText));
            Require(continueText, nameof(continueText));
        }

        /// <summary>Shows the view and starts its entrance; called once per conversation, before its first line.</summary>
        public virtual void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        /// <summary>Shows a line with no characters visible yet.</summary>
        /// <returns>The number of characters the line reveals (rich-text tags excluded).</returns>
        public int ShowLine(DialogueSpeakerEntry speaker, string text, bool isLastLine)
        {
            ShowSpeaker(speaker);
            lineText.maxVisibleCharacters = 0;
            lineText.SetText(text ?? string.Empty);
            continueText.SetText(isLastLine ? CLOSE_LABEL : NEXT_LABEL);
            SetContinueVisible(false);

            // the text is parsed once per line, so typing only changes the visible count
            lineText.ForceMeshUpdate(true);
            return lineText.textInfo.characterCount;
        }

        public void SetVisibleCharacters(int count)
        {
            if (lineText.maxVisibleCharacters != count)
            {
                lineText.maxVisibleCharacters = count;
            }
        }

        public void SetContinueVisible(bool isVisible)
        {
            if (continueText.gameObject.activeSelf != isVisible)
            {
                continueText.gameObject.SetActive(isVisible);
            }
        }

        /// <summary>Places the view where it was authored in the prefab.</summary>
        public abstract void PlaceAuthored();

        /// <summary>Places the view at a point of its parent, (0,0) bottom-left to (1,1) top-right, keeping it on screen.</summary>
        public abstract void PlaceAt(Vector2 normalizedPosition);

        /// <summary>Plays the entrance for the elapsed unscaled time.</summary>
        public virtual void AdvanceEntrance(float deltaSeconds)
        {
        }

        /// <summary>Ends the entrance at once.</summary>
        public virtual void CompleteEntrance()
        {
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                Clicked?.Invoke();
            }
        }

        /// <summary>Shows who speaks, if the presentation shows it.</summary>
        protected abstract void ShowSpeaker(DialogueSpeakerEntry speaker);

        protected void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException($"{GetType().Name} on '{name}' is missing '{fieldName}'. Assign it in its prefab.");
            }
        }
    }
}
