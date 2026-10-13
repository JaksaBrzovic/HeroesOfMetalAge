using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;

namespace HeroesOfMetalAge.Application
{
    public enum GameScreen
    {
        Adventure,
        Town,
        Hero,
        Battle
    }

    public enum DialogKind
    {
        None,
        Intro,
        Friendly,
        Hostile,
        Claim,
        Victory,
        /// <summary>The spoils of the battle just won, shown after the victory screen.</summary>
        Rewards,
        Defeat
    }

    /// <summary>Where the player is, for the music tracks and layers and the riding loop.</summary>
    public readonly struct PlaybackState : IEquatable<PlaybackState>
    {
        public PlaybackState(bool isRiding, bool isFighting, bool isDefeated, bool isInTown)
        {
            IsRiding = isRiding;
            IsFighting = isFighting;
            IsDefeated = isDefeated;
            IsInTown = isInTown;
        }

        public bool IsRiding { get; }
        public bool IsFighting { get; }
        /// <summary>True while the defeat screen is open.</summary>
        public bool IsDefeated { get; }
        /// <summary>True on the town screen and on the hero screen of a hero stationed in the town.</summary>
        public bool IsInTown { get; }

        public bool Equals(PlaybackState other)
        {
            return IsRiding == other.IsRiding && IsFighting == other.IsFighting && IsDefeated == other.IsDefeated && IsInTown == other.IsInTown;
        }

        public override bool Equals(object obj)
        {
            return obj is PlaybackState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (IsRiding ? 1 : 0) | (IsFighting ? 2 : 0) | (IsDefeated ? 4 : 0) | (IsInTown ? 8 : 0);
        }
    }

    /// <summary>What a won battle granted, for the victory and rewards screens.</summary>
    public sealed class VictorySummary
    {
        public VictorySummary(EncounterDefinition encounter, VictoryRewards rewards, int losses, bool hasLeveledUp)
        {
            EncounterId = encounter.Id;
            Experience = encounter.RewardExperience;
            Gold = rewards.Gold;
            ArtefactIds = rewards.ArtefactIds;
            HasLegendary = rewards.HasLegendary;
            Losses = losses;
            HasLeveledUp = hasLeveledUp;
        }

        public string EncounterId { get; }
        public int Experience { get; }
        public int Gold { get; }
        /// <summary>None or one artefact.</summary>
        public IReadOnlyList<string> ArtefactIds { get; }
        public bool HasLegendary { get; }
        public int Losses { get; }
        public bool HasLeveledUp { get; }
    }
}
