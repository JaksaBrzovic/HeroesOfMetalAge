namespace HeroesOfMetalAge.Application
{
    /// <summary>Game events that have a sound. The audio database assigns a sound to every value.</summary>
    public enum SoundCue
    {
        Friendly,
        Enemy,
        BattleIntro,
        Victory,
        Defeat,
        LevelUp,
        Gold,
        Equip,
        Click,
        Hit,
        HeavyHit,
        Hurt
    }

    /// <summary>The synchronized music layers, in mixing order.</summary>
    public enum MusicLayer
    {
        Ambient,
        Gallop,
        Battle
    }
}
