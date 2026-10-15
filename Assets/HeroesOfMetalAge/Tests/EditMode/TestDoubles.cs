using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Save store kept in memory, so tests never touch the player's campaign file.</summary>
    internal sealed class MemorySaveStore : ISaveStore
    {
        public CampaignSnapshot Snapshot { get; set; }
        public bool Exists => Snapshot != null;
        public string Location => "memory";

        public CampaignSnapshot Read()
        {
            if (Snapshot == null)
            {
                throw new InvalidOperationException("No campaign in memory.");
            }

            return Snapshot;
        }

        public void Write(CampaignSnapshot snapshot)
        {
            Snapshot = snapshot;
        }
    }

    /// <summary>Audio controls that keep volumes in memory and play nothing.</summary>
    internal sealed class FakeAudioControls : IAudioControls
    {
        public float MusicVolume { get; set; } = 0.5f;
        public float EffectsVolume { get; set; } = 0.5f;
        public float VoiceVolume { get; set; } = 0.5f;
        public bool IsMuted { get; set; }
        public bool IsRiding => false;
        public bool IsFighting => false;
        public List<SoundCue> Played { get; } = new List<SoundCue>();

        public void Play(SoundCue cue)
        {
            Played.Add(cue);
        }

        public void Audition(SoundCue cue)
        {
        }

        public void AuditionLayer(MusicLayer layer)
        {
        }

        public void AuditionMusic(MusicTrack track)
        {
        }

        public void AuditionRidingLoop()
        {
        }

        public void AuditionVoice(string unitId)
        {
        }

        public void SavePreferences()
        {
        }
    }

    /// <summary>Random source that always returns the same value.</summary>
    internal sealed class FixedRandomSource : IRandomSource
    {
        private readonly double value;

        public FixedRandomSource(double value)
        {
            this.value = value;
        }

        public double NextValue()
        {
            return value;
        }
    }

    internal static class CampaignTestUtility
    {
        /// <summary>A copy of the campaign standing on the town.</summary>
        public static CampaignState AtTown(CampaignRules rules, CampaignState state, DefinitionCatalog catalog)
        {
            CampaignSnapshot snapshot = state.Snapshot();
            WorldPosition town = catalog.Mission.Encounter(EncounterKind.Town).Position;
            snapshot.x = town.X;
            snapshot.y = town.Y;
            return rules.Restore(snapshot);
        }

        public static CampaignState OwnedTown(CampaignRules rules, CampaignState state, DefinitionCatalog catalog)
        {
            CampaignSnapshot snapshot = AtTown(rules, state, catalog).Snapshot();
            snapshot.townOwnerId = CampaignState.PLAYER_OWNER_ID;
            return rules.Restore(snapshot);
        }

        public static GameSession NewSession(DefinitionCatalog catalog, ISaveStore store, Action<Exception> reportError = null)
        {
            return new GameSession(catalog, store, new SeededRandomSource(1), 0.1f, 0.1f, 0.1f, 0.1f, 0.1f, reportError ?? (error => { }));
        }
    }
}
