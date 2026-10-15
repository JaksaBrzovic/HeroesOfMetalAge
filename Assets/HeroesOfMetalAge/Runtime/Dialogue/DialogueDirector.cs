using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>
    /// Plays conversations from the dialogue database at any time and anywhere on the screen, in the presentation each
    /// conversation names: the portrait box or the comic-strip speech balloon. Lines type at the database's speed (unscaled
    /// time, so typing continues while gameplay is paused) with their optional voice-over, after the presentation's
    /// entrance. A click on the dialogue ends the entrance and shows the rest of a typing line, then moves to the next line;
    /// after the last line it closes. Playing while a conversation runs replaces it. Gameplay is not blocked: only clicks on
    /// the dialogue itself are taken, except where the caller's condition (<see cref="SetClickAwayCondition"/>) holds:
    /// there a click anywhere else closes the dialogue as if it was read to the end, and the click goes no further. Knows
    /// nothing about the game; callers pass conversation IDs.
    /// </summary>
    public sealed class DialogueDirector : MonoBehaviour
    {
        [Tooltip("The 1920x1080 frame the dialogue is placed in.")]
        [SerializeField] private RectTransform designFrame;
        [FormerlySerializedAs("box")]
        [SerializeField] private DialogueBoxView portraitBox;
        [SerializeField] private DialogueBalloonView speechBalloon;
        [Tooltip("Invisible full-screen button behind the dialogue; active only while a click away closes it.")]
        [SerializeField] private Button dismissArea;

        private readonly Dictionary<string, DialogueConversationEntry> conversations = new Dictionary<string, DialogueConversationEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, DialogueSpeakerEntry> speakers = new Dictionary<string, DialogueSpeakerEntry>(StringComparer.Ordinal);
        private DialogueTypewriter typewriter;
        private IDialogueVoice voice;
        private DialogueConversationEntry conversation;
        private DialogueView view;
        private int lineIndex;
        private Func<bool> isClickAwayClosing;

        /// <summary>Raised with the conversation's ID after its last line is closed or a click away closed it; not for stopped or replaced conversations.</summary>
        public event Action<string> Completed;

        public bool IsPlaying => conversation != null;
        /// <summary>The conversation on screen, or null.</summary>
        public string CurrentConversationId => conversation?.Id;
        /// <summary>The line on screen, or null.</summary>
        public DialogueLineEntry CurrentLine => conversation?.Lines[lineIndex];
        public DialogueBoxView PortraitBox => portraitBox;
        public DialogueBalloonView SpeechBalloon => speechBalloon;
        public Button DismissArea => dismissArea;

        /// <exception cref="InvalidOperationException">Called twice, a reference is missing or the database is invalid.</exception>
        public void Initialize(DialogueDatabaseScriptableObject database, IDialogueVoice dialogueVoice)
        {
            if (typewriter != null)
            {
                throw new InvalidOperationException("Dialogue director was initialized twice.");
            }

            if (designFrame == null || portraitBox == null || speechBalloon == null || dismissArea == null || database == null || dialogueVoice == null)
            {
                throw new InvalidOperationException($"DialogueDirector on '{name}' needs its design frame, both views, the dismiss area, a dialogue database and a voice player.");
            }

            IReadOnlyList<string> errors = database.Validate();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException("Invalid dialogue database '" + database.name + "':\n" + string.Join("\n", errors));
            }

            // a snapshot of the authored asset; edits apply on the next start
            foreach (DialogueSpeakerEntry speaker in database.Speakers)
            {
                speakers.Add(speaker.Id, speaker);
            }

            foreach (DialogueConversationEntry entry in database.Conversations)
            {
                conversations.Add(entry.Id, entry);
            }

            voice = dialogueVoice;
            typewriter = new DialogueTypewriter(database.CharactersPerSecond);
            foreach (DialogueView each in new DialogueView[] { portraitBox, speechBalloon })
            {
                each.Initialize();
                each.Hide();
                each.Clicked += Advance;
            }

            dismissArea.onClick.AddListener(Dismiss);
            RefreshDismissArea();
        }

        /// <summary>
        /// Where a click outside the dialogue closes it, for example only on the adventure map; asked every frame while a
        /// dialogue is open. Null (the default) never closes it that way.
        /// </summary>
        public void SetClickAwayCondition(Func<bool> condition)
        {
            isClickAwayClosing = condition;
            RefreshDismissArea();
        }

        public bool HasConversation(string conversationId)
        {
            return conversationId != null && conversations.ContainsKey(conversationId);
        }

        /// <exception cref="ArgumentException">The database has no conversation with this ID.</exception>
        public DialogueStyle StyleOf(string conversationId)
        {
            return Find(conversationId).Style;
        }

        /// <summary>Plays a conversation where its presentation was authored in the prefab.</summary>
        /// <exception cref="ArgumentException">The database has no conversation with this ID.</exception>
        public void Play(string conversationId)
        {
            DialogueConversationEntry entry = Find(conversationId);
            ViewFor(entry).PlaceAuthored();
            Begin(entry);
        }

        /// <summary>
        /// Plays a conversation at a point of the screen's 1920x1080 design frame, (0,0) bottom-left to (1,1) top-right: where
        /// the portrait box sits, or where the speaker is for a speech balloon. The dialogue always stays fully on screen.
        /// </summary>
        /// <exception cref="ArgumentException">The database has no conversation with this ID.</exception>
        public void Play(string conversationId, Vector2 normalizedPosition)
        {
            DialogueConversationEntry entry = Find(conversationId);
            ViewFor(entry).PlaceAt(normalizedPosition);
            Begin(entry);
        }

        /// <summary>Plays a conversation at the centre of a UI element on any canvas, for example the speaker's portrait.</summary>
        /// <exception cref="ArgumentException">The database has no conversation with this ID.</exception>
        public void Play(string conversationId, RectTransform speaker)
        {
            if (speaker == null)
            {
                throw new ArgumentNullException(nameof(speaker));
            }

            Canvas canvas = speaker.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            PlayAtScreenPoint(conversationId, RectTransformUtility.WorldToScreenPoint(camera, speaker.TransformPoint(speaker.rect.center)));
        }

        /// <summary>Plays a conversation at a screen pixel, for example a world object's point from its camera's WorldToScreenPoint.</summary>
        /// <exception cref="ArgumentException">The database has no conversation with this ID.</exception>
        public void PlayAtScreenPoint(string conversationId, Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(designFrame, screenPoint, null, out Vector2 local);
            Play(conversationId, Rect.PointToNormalized(designFrame.rect, local));
        }

        /// <summary>Ends the entrance and shows the rest of a typing line, else moves to the next line or closes after the last one.</summary>
        public void Advance()
        {
            if (!IsPlaying)
            {
                return;
            }

            if (!view.IsEntranceComplete || !typewriter.IsComplete)
            {
                view.CompleteEntrance();
                typewriter.Complete();
                ShowProgress();
                return;
            }

            if (lineIndex < conversation.Lines.Count - 1)
            {
                lineIndex++;
                ShowLine();
                return;
            }

            string completedId = conversation.Id;
            Close();
            Completed?.Invoke(completedId);
        }

        /// <summary>Closes the dialogue as if its last line was read: raises <see cref="Completed"/>. Used by a click away.</summary>
        public void Dismiss()
        {
            if (!IsPlaying)
            {
                return;
            }

            string dismissedId = conversation.Id;
            Close();
            Completed?.Invoke(dismissedId);
        }

        /// <summary>Closes the dialogue and silences the voice-over without raising <see cref="Completed"/>.</summary>
        public void Stop()
        {
            if (IsPlaying)
            {
                Close();
            }
        }

        /// <summary>Plays the entrance, then types the current line; Update passes unscaled time, tests pass their own.</summary>
        internal void Tick(float deltaSeconds)
        {
            if (!IsPlaying)
            {
                return;
            }

            if (!view.IsEntranceComplete)
            {
                view.AdvanceEntrance(deltaSeconds);
                if (view.IsEntranceComplete)
                {
                    ShowProgress();
                }

                return;
            }

            if (typewriter.Advance(deltaSeconds))
            {
                ShowProgress();
            }
        }

        private void Update()
        {
            Tick(Time.unscaledDeltaTime);
            RefreshDismissArea();
        }

        private void OnDestroy()
        {
            foreach (DialogueView each in new DialogueView[] { portraitBox, speechBalloon })
            {
                if (each != null)
                {
                    each.Clicked -= Advance;
                }
            }

            if (dismissArea != null)
            {
                dismissArea.onClick.RemoveListener(Dismiss);
            }

            voice?.Stop();
        }

        private void RefreshDismissArea()
        {
            bool isActive = IsPlaying && isClickAwayClosing != null && isClickAwayClosing();
            if (dismissArea.gameObject.activeSelf != isActive)
            {
                dismissArea.gameObject.SetActive(isActive);
            }
        }

        private DialogueConversationEntry Find(string conversationId)
        {
            if (typewriter == null)
            {
                throw new InvalidOperationException("Dialogue director is not initialized.");
            }

            if (conversationId == null || !conversations.TryGetValue(conversationId, out DialogueConversationEntry entry))
            {
                throw new ArgumentException("No dialogue conversation '" + conversationId + "' in the dialogue database.", nameof(conversationId));
            }

            return entry;
        }

        private DialogueView ViewFor(DialogueConversationEntry entry)
        {
            return entry.Style == DialogueStyle.SpeechBalloon ? speechBalloon : (DialogueView)portraitBox;
        }

        private void Begin(DialogueConversationEntry entry)
        {
            DialogueView next = ViewFor(entry);
            if (view != null && view != next)
            {
                view.Hide();
            }

            conversation = entry;
            view = next;
            lineIndex = 0;
            view.Open();
            ShowLine();
            RefreshDismissArea();
        }

        private void ShowLine()
        {
            DialogueLineEntry line = conversation.Lines[lineIndex];
            bool isLastLine = lineIndex == conversation.Lines.Count - 1;
            int characterCount = view.ShowLine(speakers[line.SpeakerId], line.Text, isLastLine);
            typewriter.Begin(characterCount);
            ShowProgress();
            if (line.VoiceOver != null)
            {
                voice.Play(line.VoiceOver);
            }
            else
            {
                voice.Stop();
            }
        }

        private void ShowProgress()
        {
            view.SetVisibleCharacters(typewriter.VisibleCharacters);
            view.SetContinueVisible(view.IsEntranceComplete && typewriter.IsComplete);
        }

        private void Close()
        {
            conversation = null;
            lineIndex = 0;
            voice.Stop();
            view.Hide();
            RefreshDismissArea();
        }
    }
}
