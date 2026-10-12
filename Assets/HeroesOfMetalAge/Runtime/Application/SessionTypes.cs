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
        Defeat
    }

    /// <summary>Travel/combat state for the music layers.</summary>
    public readonly struct PlaybackState
    {
        public PlaybackState(bool isRiding, bool isFighting)
        {
            IsRiding = isRiding;
            IsFighting = isFighting;
        }

        public bool IsRiding { get; }
        public bool IsFighting { get; }
    }

    /// <summary>What a won battle granted, for the victory dialog.</summary>
    public sealed class VictorySummary
    {
        public VictorySummary(EncounterDefinition encounter, int losses, bool hasLeveledUp)
        {
            EncounterId = encounter.Id;
            Experience = encounter.RewardExperience;
            Gold = encounter.RewardGold;
            ArtefactId = encounter.RewardArtefactId;
            Losses = losses;
            HasLeveledUp = hasLeveledUp;
        }

        public string EncounterId { get; }
        public int Experience { get; }
        public int Gold { get; }
        public string ArtefactId { get; }
        public int Losses { get; }
        public bool HasLeveledUp { get; }
    }
}
