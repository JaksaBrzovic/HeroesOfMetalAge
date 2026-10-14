using System;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>Reveals a line's characters at a fixed rate. Plain C#, so the timing is testable without frames.</summary>
    public sealed class DialogueTypewriter
    {
        private const float CHARACTER_TOLERANCE = 0.001f;

        private readonly float charactersPerSecond;

        private int characterCount;
        private float elapsedSeconds;

        /// <exception cref="ArgumentOutOfRangeException">The rate is not positive.</exception>
        public DialogueTypewriter(float charactersPerSecond)
        {
            if (charactersPerSecond <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(charactersPerSecond), charactersPerSecond, "Typing speed must be positive.");
            }

            this.charactersPerSecond = charactersPerSecond;
        }

        public int VisibleCharacters { get; private set; }
        public bool IsComplete => VisibleCharacters >= characterCount;

        /// <summary>Starts a line with nothing visible.</summary>
        public void Begin(int lineCharacterCount)
        {
            characterCount = Math.Max(0, lineCharacterCount);
            elapsedSeconds = 0f;
            VisibleCharacters = 0;
        }

        /// <returns>True when more characters became visible.</returns>
        public bool Advance(float deltaSeconds)
        {
            if (IsComplete || deltaSeconds <= 0f)
            {
                return false;
            }

            elapsedSeconds += deltaSeconds;
            // the tolerance keeps float error from holding back a character that is due exactly now
            int visible = (int)Math.Min(characterCount, Math.Floor(elapsedSeconds * charactersPerSecond + CHARACTER_TOLERANCE));
            if (visible == VisibleCharacters)
            {
                return false;
            }

            VisibleCharacters = visible;
            return true;
        }

        /// <summary>Shows the rest of the line at once.</summary>
        public void Complete()
        {
            VisibleCharacters = characterCount;
        }
    }
}
