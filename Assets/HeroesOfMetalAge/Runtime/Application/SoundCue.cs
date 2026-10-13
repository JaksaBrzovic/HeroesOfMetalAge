namespace HeroesOfMetalAge.Application
{
    /// <summary>Game events that have a sound. The audio database assigns a sound to every value.</summary>
    public enum SoundCue
    {
        /// <summary>Mission start, entering a town and a new week.</summary>
        Friendly,
        FriendlyEncounter,
        EnemyEncounter,
        Victory,
        LevelUp,
        Gold,
        LegendaryItem,
        Equip,
        Click,
        SwordSwing,
        SwordHit,
        SwordMiss,
        ArrowFire,
        ArrowHit,
        ArrowMiss,
        /// <summary>Layered over a sword or arrow hit that deals heavy damage.</summary>
        HeavyHit
    }

    /// <summary>The synchronized music layers, in mixing order.</summary>
    public enum MusicLayer
    {
        Ambient,
        Gallop
    }

    /// <summary>Music that leads over the layers while a state lasts.</summary>
    public enum MusicTrack
    {
        /// <summary>The mission's adventure map.</summary>
        Map,
        /// <summary>The mission's town.</summary>
        Town,
        /// <summary>Battle intro followed by the battle loop.</summary>
        Battle,
        /// <summary>Played once after the victory stinger.</summary>
        Victory,
        Defeat
    }
}
