using System;
using HeroesOfMetalAge.Application;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>The front end: timing of New Game, Load Game, Credits and Quit, the intro's text and the credits.</summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Front End Settings")]
    public sealed class FrontEndSettingsScriptableObject : ScriptableObject
    {
        [Tooltip("Seconds the main menu and the music take to fade to black after New Game.")]
        [Min(0f)]
        [SerializeField] private float fadeOutSeconds = 1.5f;
        [Tooltip("Seconds of black silence before the intro (New Game) or the burn (Load Game).")]
        [Min(0f)]
        [SerializeField] private float blackSeconds = 2f;
        [Tooltip("Seconds from Load Game to black: its text falls into the hole and the camera falls in after it.")]
        [Min(0f)]
        [SerializeField] private float loadGameOutSeconds = 3.2f;

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

        [Header("Credits")]
        [Tooltip("Seconds the scroll takes to unroll and the credits screen to fade in.")]
        [Min(0f)]
        [SerializeField] private float creditsOpenSeconds = 1.3f;
        [Tooltip("Seconds the credits screen takes to fade out back to the menu.")]
        [Min(0f)]
        [SerializeField] private float creditsCloseSeconds = 0.8f;
        [Tooltip("The rolling credits; TextMeshPro rich text.")]
        [TextArea(10, 40)]
        [SerializeField] private string credits = string.Empty;

        [Header("Quit")]
        [Tooltip("Seconds the rider takes to leave before the program closes; the music fades out with him.")]
        [Min(0f)]
        [SerializeField] private float quitSeconds = 1.2f;

        public string Credits => credits ?? string.Empty;

        public FrontEndTiming CreateTiming()
        {
            return new FrontEndTiming(fadeOutSeconds, loadGameOutSeconds, blackSeconds, burnSeconds, musicFadeInSeconds, creditsOpenSeconds,
                creditsCloseSeconds, quitSeconds);
        }

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
