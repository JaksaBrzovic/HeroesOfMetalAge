using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Application
{
    /// <summary>Campaign save storage. Implementations must keep the last valid save if a write fails.</summary>
    public interface ISaveStore
    {
        bool Exists { get; }
        string Location { get; }

        CampaignSnapshot Read();
        void Write(CampaignSnapshot snapshot);
    }

    /// <summary>Player audio settings and diagnostics, implemented by the audio adapter.</summary>
    public interface IAudioControls
    {
        float MusicVolume { get; set; }
        float EffectsVolume { get; set; }
        float VoiceVolume { get; set; }
        bool IsMuted { get; set; }
        /// <summary>True while the hero rides and the riding loop plays.</summary>
        bool IsRiding { get; }
        /// <summary>True while fighting (the battle music leads).</summary>
        bool IsFighting { get; }

        /// <summary>Plays an interface sound that belongs to no gameplay event, such as a main menu button's.</summary>
        void Play(SoundCue cue);

        // review sounds without replaying the mission (development panel)
        void Audition(SoundCue cue);
        /// <summary>Plays one pass of a music layer as a preview.</summary>
        void AuditionLayer(MusicLayer layer);
        /// <summary>Plays the beginning of a music track as a preview.</summary>
        void AuditionMusic(MusicTrack track);
        /// <summary>Plays the beginning of the riding loop as a preview.</summary>
        void AuditionRidingLoop();
        void AuditionVoice(string unitId);
        void SavePreferences();
    }
}
