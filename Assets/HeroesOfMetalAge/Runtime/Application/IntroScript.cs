using System;
using System.Collections.Generic;

namespace HeroesOfMetalAge.Application
{
    /// <summary>One line of an intro stanza, typed letter by letter.</summary>
    public sealed class IntroLine
    {
        public IntroLine(string text, float pauseAfterSeconds)
        {
            Text = text ?? string.Empty;
            PauseAfterSeconds = Math.Max(0f, pauseAfterSeconds);
        }

        public string Text { get; }
        /// <summary>Seconds after the line is typed before the next line starts; unused after the last line.</summary>
        public float PauseAfterSeconds { get; }
    }

    /// <summary>Lines shown together in the middle of the screen, held once typed, then faded out.</summary>
    public sealed class IntroStanza
    {
        public IntroStanza(IReadOnlyList<IntroLine> lines, float holdSeconds)
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            HoldSeconds = Math.Max(0f, holdSeconds);
        }

        public IReadOnlyList<IntroLine> Lines { get; }
        /// <summary>Seconds the fully typed stanza stays before it fades out.</summary>
        public float HoldSeconds { get; }
    }

    /// <summary>
    /// The intro's timeline on black: the stanzas one after another (each line typed letter by letter, the stanza held,
    /// faded out, a short black gap), then the title fading in slowly and held. It only answers what is visible at a time;
    /// <see cref="FrontEndFlow"/> plays the verses and the title as two phases and the view draws them.
    /// </summary>
    public sealed class IntroScript
    {
        /// <summary>No verses and no title: both phases take no time.</summary>
        public static readonly IntroScript None = new IntroScript(Array.Empty<IntroStanza>(), string.Empty, 1f, 0f, 0f, 0f, 0f);

        private readonly float charactersPerSecond;
        private readonly float stanzaFadeSeconds;
        private readonly float titleFadeInSeconds;
        private readonly float titleHoldSeconds;
        private readonly float[] stanzaStarts;
        private readonly float[] fadeStarts;
        private readonly float[][] lineStarts;

        /// <param name="stanzaGapSeconds">Black after each stanza has faded out, the last one included.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="charactersPerSecond"/> is not positive.</exception>
        public IntroScript(IReadOnlyList<IntroStanza> stanzas, string title, float charactersPerSecond, float stanzaFadeSeconds,
            float stanzaGapSeconds, float titleFadeInSeconds, float titleHoldSeconds)
        {
            if (!(charactersPerSecond > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(charactersPerSecond), charactersPerSecond, "The intro types at least some characters per second.");
            }

            Stanzas = stanzas ?? throw new ArgumentNullException(nameof(stanzas));
            Title = title ?? string.Empty;
            this.charactersPerSecond = charactersPerSecond;
            this.stanzaFadeSeconds = Math.Max(0f, stanzaFadeSeconds);
            this.titleFadeInSeconds = Math.Max(0f, titleFadeInSeconds);
            this.titleHoldSeconds = Math.Max(0f, titleHoldSeconds);
            float gap = Math.Max(0f, stanzaGapSeconds);

            stanzaStarts = new float[stanzas.Count];
            fadeStarts = new float[stanzas.Count];
            lineStarts = new float[stanzas.Count][];
            float time = 0f;
            for (int s = 0; s < stanzas.Count; s++)
            {
                IReadOnlyList<IntroLine> lines = stanzas[s].Lines;
                stanzaStarts[s] = time;
                lineStarts[s] = new float[lines.Count];
                for (int l = 0; l < lines.Count; l++)
                {
                    lineStarts[s][l] = time;
                    time += TypingSeconds(lines[l]);
                    if (l < lines.Count - 1)
                    {
                        time += lines[l].PauseAfterSeconds;
                    }
                }

                time += stanzas[s].HoldSeconds;
                fadeStarts[s] = time;
                time += this.stanzaFadeSeconds + gap;
            }

            VersesSeconds = time;
        }

        public IReadOnlyList<IntroStanza> Stanzas { get; }
        public string Title { get; }
        /// <summary>Length of all the stanzas with their holds, fades and gaps.</summary>
        public float VersesSeconds { get; }
        /// <summary>Length of the title's fade in and hold.</summary>
        public float TitleSeconds => titleFadeInSeconds + titleHoldSeconds;
        /// <summary>The most lines any stanza has.</summary>
        public int MaxLines
        {
            get
            {
                int max = 0;
                foreach (IntroStanza stanza in Stanzas)
                {
                    max = Math.Max(max, stanza.Lines.Count);
                }

                return max;
            }
        }

        /// <param name="versesTime">Seconds since the verses started.</param>
        /// <returns>The stanza on screen, or -1 in the black gaps.</returns>
        public int StanzaAt(float versesTime)
        {
            for (int s = Stanzas.Count - 1; s >= 0; s--)
            {
                if (versesTime >= stanzaStarts[s])
                {
                    return versesTime < fadeStarts[s] + stanzaFadeSeconds ? s : -1;
                }
            }

            return -1;
        }

        /// <returns>How many characters of the line are typed: the first appears as the line starts, all of them once it is done.</returns>
        public int VisibleCharacters(int stanza, int line, float versesTime)
        {
            float start = lineStarts[stanza][line];
            if (versesTime < start)
            {
                return 0;
            }

            int length = Stanzas[stanza].Lines[line].Text.Length;
            double typed = Math.Floor((versesTime - start) * charactersPerSecond) + 1d;
            return (int)Math.Min(length, typed);
        }

        /// <returns>1 while the stanza is typed and held, falling to 0 over its fade out.</returns>
        public float StanzaAlpha(int stanza, float versesTime)
        {
            float faded = versesTime - fadeStarts[stanza];
            if (faded <= 0f)
            {
                return 1f;
            }

            return stanzaFadeSeconds > 0f ? Math.Max(0f, 1f - faded / stanzaFadeSeconds) : 0f;
        }

        /// <param name="titleTime">Seconds since the title started.</param>
        public float TitleAlpha(float titleTime)
        {
            return titleFadeInSeconds > 0f ? Math.Min(1f, Math.Max(0f, titleTime / titleFadeInSeconds)) : 1f;
        }

        private float TypingSeconds(IntroLine line)
        {
            return line.Text.Length / charactersPerSecond;
        }
    }
}
