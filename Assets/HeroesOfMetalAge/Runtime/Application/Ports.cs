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
        /// <summary>True while the gallop layer leads the mix.</summary>
        bool IsRiding { get; }
        /// <summary>True while the battle layer leads the mix.</summary>
        bool IsFighting { get; }

        // review sounds without replaying the mission (development panel)
        void Audition(SoundCue cue);
        /// <summary>Plays one pass of a music layer as a preview.</summary>
        void AuditionLayer(MusicLayer layer);
        void AuditionVoice(string unitId);
        void SavePreferences();
    }
}
