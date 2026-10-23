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
        Defeat,
        /// <summary>A treasure chest: take its gold or its experience.</summary>
        Chest,
        /// <summary>A dwelling's creatures for sale.</summary>
        Dwelling,
        /// <summary>The exit portal asks whether to finish the mission.</summary>
        Exit,
        /// <summary>The hero gained a level: a primary stat rose and a secondary skill is to be chosen.</summary>
        LevelUp,
        /// <summary>The hero's spell book is open, on the map, the hero screen or in battle.</summary>
        SpellBook,
        /// <summary>A spell shop sells its spell book and spells.</summary>
        SpellShop
    }

    /// <summary>Where the player is, for the music tracks and layers and the riding loop.</summary>
    public readonly struct PlaybackState : IEquatable<PlaybackState>
    {
        public PlaybackState(bool isRiding, bool isFighting, bool isDefeated, bool isInTown, bool isCelebrating)
        {
            IsRiding = isRiding;
            IsFighting = isFighting;
            IsDefeated = isDefeated;
            IsInTown = isInTown;
            IsCelebrating = isCelebrating;
        }

        public bool IsRiding { get; }
        public bool IsFighting { get; }
        /// <summary>True while the defeat screen is open.</summary>
        public bool IsDefeated { get; }
        /// <summary>True on the town screen and on the hero screen of a hero stationed in the town.</summary>
        public bool IsInTown { get; }
        /// <summary>True while the victory or rewards screen of a won battle is open.</summary>
        public bool IsCelebrating { get; }

        public bool Equals(PlaybackState other)
        {
            return IsRiding == other.IsRiding && IsFighting == other.IsFighting && IsDefeated == other.IsDefeated && IsInTown == other.IsInTown
                && IsCelebrating == other.IsCelebrating;
        }

        public override bool Equals(object obj)
        {
            return obj is PlaybackState other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (IsRiding ? 1 : 0) | (IsFighting ? 2 : 0) | (IsDefeated ? 4 : 0) | (IsInTown ? 8 : 0) | (IsCelebrating ? 16 : 0);
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

    /// <summary>The creatures each side lost in a battle, one entry per unit type.</summary>
    public sealed class BattleCasualties
    {
        public static readonly BattleCasualties None = new BattleCasualties(new Troop[0], new Troop[0]);

        public BattleCasualties(IEnumerable<Troop> player, IEnumerable<Troop> enemy)
        {
            Player = new List<Troop>(player).AsReadOnly();
            Enemy = new List<Troop>(enemy).AsReadOnly();
        }

        public IReadOnlyList<Troop> Player { get; }
        public IReadOnlyList<Troop> Enemy { get; }
    }
}
