using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>
    /// Every speaker and conversation of the game and the typing speed. Lines name their speaker by ID and hold their
    /// voice-over as a direct SoundDefinition reference. Edits apply on the next Play Mode start or build.
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Dialogue Database")]
    public sealed class DialogueDatabaseScriptableObject : ScriptableObject
    {
        public const float MINIMUM_CHARACTERS_PER_SECOND = 1f;

        [Tooltip("How fast lines type, in characters per second. A click shows the rest of the line at once.")]
        [Min(MINIMUM_CHARACTERS_PER_SECOND)]
        [SerializeField] private float charactersPerSecond = 80f;
        [SerializeField] private List<DialogueSpeakerEntry> speakers = new List<DialogueSpeakerEntry>();
        [SerializeField] private List<DialogueConversationEntry> conversations = new List<DialogueConversationEntry>();

        public float CharactersPerSecond
        {
            get => charactersPerSecond;
            internal set => charactersPerSecond = value;
        }

        public IReadOnlyList<DialogueSpeakerEntry> Speakers => speakers;
        public IReadOnlyList<DialogueConversationEntry> Conversations => conversations;

        /// <returns>Every problem found, each naming the offending entry; empty when the database is valid.</returns>
        public IReadOnlyList<string> Validate()
        {
            List<string> errors = new List<string>();
            if (charactersPerSecond < MINIMUM_CHARACTERS_PER_SECOND)
            {
                errors.Add($"Characters per second must be at least {MINIMUM_CHARACTERS_PER_SECOND}.");
            }

            HashSet<string> speakerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DialogueSpeakerEntry speaker in speakers)
            {
                if (speaker == null || string.IsNullOrWhiteSpace(speaker.Id))
                {
                    errors.Add("A speaker has no ID.");
                }
                else if (!speakerIds.Add(speaker.Id))
                {
                    errors.Add("Duplicate speaker ID '" + speaker.Id + "'.");
                }
            }

            HashSet<string> conversationIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> lineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DialogueConversationEntry conversation in conversations)
            {
                if (conversation == null || string.IsNullOrWhiteSpace(conversation.Id))
                {
                    errors.Add("A conversation has no ID.");
                    continue;
                }

                if (!conversationIds.Add(conversation.Id))
                {
                    errors.Add("Duplicate conversation ID '" + conversation.Id + "'.");
                }

                if (conversation.Lines.Count == 0)
                {
                    errors.Add("Conversation '" + conversation.Id + "' has no lines.");
                }

                foreach (DialogueLineEntry line in conversation.Lines)
                {
                    ValidateLine(conversation.Id, line, speakerIds, lineIds, errors);
                }
            }

            return errors;
        }

        internal void SetContent(IEnumerable<DialogueSpeakerEntry> speakerEntries, IEnumerable<DialogueConversationEntry> conversationEntries)
        {
            speakers = new List<DialogueSpeakerEntry>(speakerEntries);
            conversations = new List<DialogueConversationEntry>(conversationEntries);
        }

        private static void ValidateLine(string conversationId, DialogueLineEntry line, HashSet<string> speakerIds, HashSet<string> lineIds, List<string> errors)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Id))
            {
                errors.Add("A line in conversation '" + conversationId + "' has no ID.");
                return;
            }

            if (!lineIds.Add(line.Id))
            {
                errors.Add("Duplicate line ID '" + line.Id + "'.");
            }

            if (line.SpeakerId == null || !speakerIds.Contains(line.SpeakerId))
            {
                errors.Add("Line '" + line.Id + "' names an unknown speaker '" + line.SpeakerId + "'.");
            }

            if (string.IsNullOrWhiteSpace(line.Text))
            {
                errors.Add("Line '" + line.Id + "' has no text.");
            }

            if (line.VoiceOver != null && !line.VoiceOver.HasClips)
            {
                errors.Add("Line '" + line.Id + "' has a voice-over without clips.");
            }
        }
    }
}
