using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>The sequence from the main menu's New Game into the mission: its timing and the intro's text.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Front End Settings")]
    public sealed class FrontEndSettingsScriptableObject : ScriptableObject
    {
        [Tooltip("Seconds the main menu and the music take to fade to black after New Game.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 1.5f;
        [Tooltip("Seconds of black silence before the intro.")]
        [Min(0f)]
        [SerializeField] private float blackSeconds = 2f;

        [Header("Intro verses")]
        [Tooltip("How fast the lines are typed.")]
        [Min(0.1f)]
        [SerializeField] private float charactersPerSecond = 15f;
        [Tooltip("Seconds a stanza takes to fade out after its hold.")]
        [Min(0f)]
        [SerializeField] private float stanzaFadeSeconds = 1.5f;
        [Tooltip("Seconds of black after each stanza, before the next one or the title.")]
        [Min(0f)]
        [SerializeField] private float stanzaGapSeconds = 1f;
        [SerializeField] private Stanza[] stanzas = Array.Empty<Stanza>();

        [Header("Intro title")]
        [SerializeField] private string title = "The Secret of Metal";
        [Tooltip("Seconds the title takes to fade in; the map music starts with it.")]
        [Min(0f)]
        [SerializeField] private float titleFadeInSeconds = 4f;
        [Tooltip("Seconds the fully visible title stays before the screen burns away.")]
        [Min(0f)]
        [SerializeField] private float titleHoldSeconds = 2.5f;
        [Tooltip("Seconds the map music takes to fade in, starting with the title.")]
        [Min(0f)]
        [SerializeField] private float musicFadeInSeconds = 10f;
        [Tooltip("Seconds the black with the title takes to burn away and uncover the mission.")]
        [Min(0f)]
        [SerializeField] private float burnSeconds = 3f;

        public float FadeOutSeconds => fadeOutSeconds;
        public float BlackSeconds => blackSeconds;
        public float MusicFadeInSeconds => musicFadeInSeconds;
        public float BurnSeconds => burnSeconds;

        /// <summary>A runtime copy of the authored intro; changing it never changes this asset.</summary>
        public IntroScript CreateIntroScript()
        {
            IntroStanza[] copies = new IntroStanza[stanzas.Length];
            for (int s = 0; s < stanzas.Length; s++)
            {
                Line[] lines = stanzas[s].Lines;
                IntroLine[] lineCopies = new IntroLine[lines.Length];
                for (int l = 0; l < lines.Length; l++)
                {
                    lineCopies[l] = new IntroLine(lines[l].Text, lines[l].PauseAfterSeconds);
                }

                copies[s] = new IntroStanza(lineCopies, stanzas[s].HoldSeconds);
            }

            return new IntroScript(copies, title, charactersPerSecond, stanzaFadeSeconds, stanzaGapSeconds, titleFadeInSeconds, titleHoldSeconds);
        }

        [Serializable]
        private sealed class Stanza
        {
            [SerializeField] private Line[] lines = Array.Empty<Line>();
            [Tooltip("Seconds the fully typed stanza stays before it fades out.")]
            [Min(0f)]
            [SerializeField] private float holdSeconds = 5f;

            public Line[] Lines => lines ?? Array.Empty<Line>();
            public float HoldSeconds => holdSeconds;
        }

        [Serializable]
        private sealed class Line
        {
            [SerializeField] private string text;
            [Tooltip("Seconds after this line is typed before the next one starts.")]
            [Min(0f)]
            [SerializeField] private float pauseAfterSeconds = 0.8f;

            public string Text => text;
            public float PauseAfterSeconds => pauseAfterSeconds;
        }
    }
}
