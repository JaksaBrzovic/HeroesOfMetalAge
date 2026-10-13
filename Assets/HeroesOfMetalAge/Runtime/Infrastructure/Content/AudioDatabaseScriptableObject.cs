using System;
using HeroesOfMetalAge.Application;
using MLin.Audio;
using UnityEngine;
using UnityEngine.Serialization;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// The sounds of game events that belong to no single unit: the music layers, the battle and defeat music, the riding
    /// loop and one sound per <see cref="SoundCue"/>, plus the audio system settings, how the layers are mixed for each
    /// travel state and the timing of strike sounds. Unit voices live on the units. Clips, variations, volume, pitch,
    /// priority, instance limit, cooldown and channel are set on each SoundDefinition.
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Audio Database")]
    public sealed class AudioDatabaseScriptableObject : ScriptableObject
    {
        [Tooltip("Mixer, volume channels (Master, Music, Effects, Voice), voice-over-music ducking and focus muting.")]
        [SerializeField] private AudioSystemSettings settings;

        [Header("Music layers (looping, started together)")]
        [SerializeField] private SoundDefinition ambientLayer;
        [SerializeField] private SoundDefinition gallopLayer;

        [Header("Music tracks")]
        [Tooltip("Plays while fighting: the intro clip once, then the looping battle theme.")]
        [FormerlySerializedAs("battleLayer")]
        [SerializeField] private SoundDefinition battleMusic;
        [Tooltip("Plays once after the victory stinger, then the map or town music returns.")]
        [SerializeField] private SoundDefinition victoryMusic;
        [Tooltip("Seconds from the victory stinger to the start of the victory music (about the stinger's length).")]
        [Min(0f)]
        [SerializeField] private float victoryMusicDelaySeconds = 1.5f;
        [Min(0f)]
        [SerializeField] private float victoryMusicFadeInSeconds = 2.5f;
        [Tooltip("The victory music fades out over its last seconds while the map or town music fades back in.")]
        [Min(0f)]
        [SerializeField] private float victoryMusicFadeOutSeconds = 4f;
        [Tooltip("Plays while the defeat screen is open.")]
        [FormerlySerializedAs("defeat")]
        [SerializeField] private SoundDefinition defeatMusic;

        [Header("Map music")]
        [Tooltip("Map music level while the hero stands (1 = its full volume, reached while riding).")]
        [Range(0f, 1f)]
        [SerializeField] private float mapMusicIdleLevel = 0.35f;
        [Tooltip("Seconds for the map music to rise when the hero starts riding and to fall when he stops.")]
        [Min(0f)]
        [SerializeField] private float mapMusicRideFadeSeconds = 0.4f;

        [Header("Movement")]
        [Tooltip("Looping hoofbeats while the hero rides.")]
        [SerializeField] private SoundDefinition ridingLoop;
        [Min(0f)]
        [SerializeField] private float ridingFadeSeconds = 0.25f;

        [Header("Encounters and results")]
        [Tooltip("Mission start, entering a town and a new week.")]
        [SerializeField] private SoundDefinition friendly;
        [SerializeField] private SoundDefinition friendlyEncounter;
        [FormerlySerializedAs("enemy")]
        [SerializeField] private SoundDefinition enemyEncounter;
        [SerializeField] private SoundDefinition victory;
        [SerializeField] private SoundDefinition levelUp;

        [Header("Rewards and feedback")]
        [Tooltip("Gold received, recruiting and the rewards screen.")]
        [SerializeField] private SoundDefinition gold;
        [SerializeField] private SoundDefinition legendaryItem;
        [SerializeField] private SoundDefinition equip;
        [SerializeField] private SoundDefinition click;

        [Header("Strikes")]
        [SerializeField] private SoundDefinition swordSwing;
        [SerializeField] private SoundDefinition swordHit;
        [SerializeField] private SoundDefinition swordMiss;
        [SerializeField] private SoundDefinition arrowFire;
        [SerializeField] private SoundDefinition arrowHit;
        [SerializeField] private SoundDefinition arrowMiss;
        [Tooltip("Layered over a hit at or above the heavy-hit threshold.")]
        [SerializeField] private SoundDefinition heavyHit;
        [Tooltip("Damage at or above this adds the heavy hit.")]
        [Min(1)]
        [SerializeField] private int heavyHitThreshold = 45;
        [Tooltip("Seconds from a sword swing to its hit or miss.")]
        [Min(0f)]
        [SerializeField] private float meleeImpactSeconds = 0.12f;
        [Tooltip("Seconds from an arrow's release to its hit or miss.")]
        [Min(0f)]
        [SerializeField] private float rangedImpactSeconds = 0.3f;
        [Tooltip("Seconds between strikes resolved together (an attack and its retaliation), so they do not blur.")]
        [Min(0f)]
        [SerializeField] private float strikeSpacingSeconds = 0.45f;

        [Header("Layer mix (0..1 of each layer's volume)")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientAtRest = 0.7f;
        [Range(0f, 1f)]
        [SerializeField] private float ambientWhileRiding = 0.4f;
        [Tooltip("Ambient level under a music track (map, town, battle, defeat).")]
        [FormerlySerializedAs("ambientInBattle")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientUnderMusic;
        [Range(0f, 1f)]
        [SerializeField] private float gallopMix = 0.72f;
        [Tooltip("Seconds for the layers to move to a new state's mix and for music tracks to crossfade.")]
        [Min(0f)]
        [SerializeField] private float layerFadeSeconds = 0.6f;

        public AudioSystemSettings Settings
        {
            get => settings;
            internal set => settings = value;
        }

        public float VictoryMusicDelaySeconds => victoryMusicDelaySeconds;
        public float VictoryMusicFadeInSeconds => victoryMusicFadeInSeconds;
        public float VictoryMusicFadeOutSeconds => victoryMusicFadeOutSeconds;
        public float MapMusicIdleLevel => mapMusicIdleLevel;
        public float MapMusicRideFadeSeconds => mapMusicRideFadeSeconds;
        public SoundDefinition RidingLoop => ridingLoop;
        public float RidingFadeSeconds => ridingFadeSeconds;
        public int HeavyHitThreshold => heavyHitThreshold;
        public float MeleeImpactSeconds => meleeImpactSeconds;
        public float RangedImpactSeconds => rangedImpactSeconds;
        public float StrikeSpacingSeconds => strikeSpacingSeconds;
        public float AmbientAtRest => ambientAtRest;
        public float AmbientWhileRiding => ambientWhileRiding;
        public float AmbientUnderMusic => ambientUnderMusic;
        public float GallopMix => gallopMix;
        public float LayerFadeSeconds => layerFadeSeconds;

        /// <returns>The assigned sound, or null when the field is empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The cue has no field.</exception>
        public SoundDefinition Sound(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Friendly:
                    return friendly;
                case SoundCue.FriendlyEncounter:
                    return friendlyEncounter;
                case SoundCue.EnemyEncounter:
                    return enemyEncounter;
                case SoundCue.Victory:
                    return victory;
                case SoundCue.LevelUp:
                    return levelUp;
                case SoundCue.Gold:
                    return gold;
                case SoundCue.LegendaryItem:
                    return legendaryItem;
                case SoundCue.Equip:
                    return equip;
                case SoundCue.Click:
                    return click;
                case SoundCue.SwordSwing:
                    return swordSwing;
                case SoundCue.SwordHit:
                    return swordHit;
                case SoundCue.SwordMiss:
                    return swordMiss;
                case SoundCue.ArrowFire:
                    return arrowFire;
                case SoundCue.ArrowHit:
                    return arrowHit;
                case SoundCue.ArrowMiss:
                    return arrowMiss;
                case SoundCue.HeavyHit:
                    return heavyHit;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue, "No sound field for this cue.");
            }
        }

        /// <exception cref="ArgumentOutOfRangeException">The layer has no field.</exception>
        public SoundDefinition Layer(MusicLayer layer)
        {
            switch (layer)
            {
                case MusicLayer.Ambient:
                    return ambientLayer;
                case MusicLayer.Gallop:
                    return gallopLayer;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "No sound field for this layer.");
            }
        }

        /// <summary>The battle, victory and defeat music; the map and town music belong to the mission.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The track has no field here.</exception>
        public SoundDefinition Music(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Battle:
                    return battleMusic;
                case MusicTrack.Victory:
                    return victoryMusic;
                case MusicTrack.Defeat:
                    return defeatMusic;
                default:
                    throw new ArgumentOutOfRangeException(nameof(track), track, "The audio database has no field for this track; map and town music are on the mission.");
            }
        }
    }
}
