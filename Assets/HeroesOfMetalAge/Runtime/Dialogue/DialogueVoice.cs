using MLin.Audio;

namespace HeroesOfMetalAge.Dialogue
{
    /// <summary>Plays line voice-overs for the dialogue director; tests replace it so no audio system is needed.</summary>
    public interface IDialogueVoice
    {
        /// <summary>Plays a voice-over, replacing the one still playing.</summary>
        void Play(SoundDefinition voiceOver);

        void Stop();
    }

    /// <summary>
    /// Plays voice-overs through the MLin audio system, which the game audio initializes before dialogue starts. Music
    /// ducks under them when their SoundDefinition is routed to the Voice channel.
    /// </summary>
    public sealed class AudioSystemDialogueVoice : IDialogueVoice
    {
        private const float STOP_FADE_SECONDS = 0.1f;

        private SoundHandle handle;

        public void Play(SoundDefinition voiceOver)
        {
            handle.Stop(STOP_FADE_SECONDS);
            handle = AudioSystem.Play(voiceOver);
        }

        public void Stop()
        {
            handle.Stop(STOP_FADE_SECONDS);
            handle = SoundHandle.Invalid;
        }
    }
}
