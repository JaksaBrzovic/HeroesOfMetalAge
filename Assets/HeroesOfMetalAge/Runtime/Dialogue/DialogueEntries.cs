using System;
using System.Collections.Generic;
using MLin.Audio;
using UnityEngine;

namespace HeroesOfMetalAge.Dialogue
{
    // Authored dialogue rows. Setters are internal so the asset seeder and tests can author content; designers edit the asset.

    /// <summary>How a conversation is presented.</summary>
    public enum DialogueStyle
    {
        /// <summary>A dialogue box with the speaker's portrait on the left and the name and line on the right.</summary>
        PortraitBox,
        /// <summary>A comic-strip balloon: a trail of small bubbles from the speaker to a cloud with the line; no portrait or name.</summary>
        SpeechBalloon
    }

    /// <summary>Someone who speaks: shown by name and portrait in the dialogue box.</summary>
    [Serializable]
    public sealed class DialogueSpeakerEntry
    {
        [Tooltip("Stable ID that lines use to name their speaker.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [Tooltip("Shown on the left of the dialogue box; the box's fallback portrait is used when empty.")]
        [SerializeField] private Sprite portrait;

        public string Id
        {
            get => id;
            internal set => id = value;
        }

        public string DisplayName
        {
            get => displayName;
            internal set => displayName = value;
        }

        public Sprite Portrait
        {
            get => portrait;
            internal set => portrait = value;
        }
    }

    /// <summary>One box of text said by one speaker, with an optional recorded voice-over.</summary>
    [Serializable]
    public sealed class DialogueLineEntry
    {
        [Tooltip("Stable ID, unique across the database. Voice-over scripts and future localization tables refer to the line by it.")]
        [SerializeField] private string id;
        [Tooltip("ID of a speaker in this database.")]
        [SerializeField] private string speakerId;
        [TextArea(2, 5)]
        [SerializeField] private string text;
        [Tooltip("Optional recorded line, played while the text types. Route it to the Voice channel so music ducks under it.")]
        [SerializeField] private SoundDefinition voiceOver;

        public string Id
        {
            get => id;
            internal set => id = value;
        }

        public string SpeakerId
        {
            get => speakerId;
            internal set => speakerId = value;
        }

        public string Text
        {
            get => text;
            internal set => text = value;
        }

        /// <summary>The recorded line, or null for text only.</summary>
        public SoundDefinition VoiceOver
        {
            get => voiceOver;
            internal set => voiceOver = value;
        }
    }

    /// <summary>Lines shown one after another in a single dialogue box; a single remark is a conversation of one line.</summary>
    [Serializable]
    public sealed class DialogueConversationEntry
    {
        [Tooltip("Stable ID that game code passes to the dialogue director.")]
        [SerializeField] private string id;
        [Tooltip("Portrait box, or a comic-strip speech balloon rising from the speaker's position.")]
        [SerializeField] private DialogueStyle style;
        [SerializeField] private List<DialogueLineEntry> lines = new List<DialogueLineEntry>();

        public string Id
        {
            get => id;
            internal set => id = value;
        }

        public DialogueStyle Style
        {
            get => style;
            internal set => style = value;
        }

        public IReadOnlyList<DialogueLineEntry> Lines => lines;

        internal void SetLines(IEnumerable<DialogueLineEntry> entries)
        {
            lines = new List<DialogueLineEntry>(entries);
        }
    }
}
