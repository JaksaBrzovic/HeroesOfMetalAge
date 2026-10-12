using System;
using HeroesOfMetalAge.Application;
using MLin.Audio;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// The sounds of game events that belong to no single unit: the music layers and one sound per <see cref="SoundCue"/>,
    /// plus the audio system settings and how the layers are mixed for each travel state. Unit voices live on the units.
    /// Clips, variations, volume, pitch, priority, instance limit, cooldown and channel are set on each SoundDefinition.
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Audio Database")]
    public sealed class AudioDatabaseScriptableObject : ScriptableObject
    {
        [Tooltip("Mixer, volume channels (Master, Music, Effects, Voice), voice-over-music ducking and focus muting.")]
        [SerializeField] private AudioSystemSettings settings;

        [Header("Music layers (looping, started together)")]
        [SerializeField] private SoundDefinition ambientLayer;
        [SerializeField] private SoundDefinition gallopLayer;
        [SerializeField] private SoundDefinition battleLayer;

        [Header("Encounters and results")]
        [SerializeField] private SoundDefinition friendly;
        [SerializeField] private SoundDefinition enemy;
        [SerializeField] private SoundDefinition battleIntro;
        [SerializeField] private SoundDefinition victory;
        [SerializeField] private SoundDefinition defeat;
        [SerializeField] private SoundDefinition levelUp;

        [Header("Feedback")]
        [SerializeField] private SoundDefinition gold;
        [SerializeField] private SoundDefinition equip;
        [SerializeField] private SoundDefinition click;

        [Header("Hits")]
        [SerializeField] private SoundDefinition hit;
        [SerializeField] private SoundDefinition heavyHit;
        [Tooltip("Played when the player's stack takes damage.")]
        [SerializeField] private SoundDefinition hurt;
        [Tooltip("Damage at or above this plays the heavy hit instead of the normal one.")]
        [Min(1)]
        [SerializeField] private int heavyHitThreshold = 45;

        [Header("Layer mix (0..1 of each layer's volume)")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientAtRest = 0.7f;
        [Range(0f, 1f)]
        [SerializeField] private float ambientWhileRiding = 0.4f;
        [Range(0f, 1f)]
        [SerializeField] private float ambientInBattle = 0.17f;
        [Range(0f, 1f)]
        [SerializeField] private float gallopMix = 0.72f;
        [Range(0f, 1f)]
        [SerializeField] private float battleMix = 0.85f;
        [Tooltip("Seconds for the layers to move to a new state's mix.")]
        [Min(0f)]
        [SerializeField] private float layerFadeSeconds = 0.6f;

        public AudioSystemSettings Settings
        {
            get => settings;
            internal set => settings = value;
        }

        public int HeavyHitThreshold => heavyHitThreshold;
        public float AmbientAtRest => ambientAtRest;
        public float AmbientWhileRiding => ambientWhileRiding;
        public float AmbientInBattle => ambientInBattle;
        public float GallopMix => gallopMix;
        public float BattleMix => battleMix;
        public float LayerFadeSeconds => layerFadeSeconds;

        /// <returns>The assigned sound, or null when the field is empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The cue has no field.</exception>
        public SoundDefinition Sound(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.Friendly:
                    return friendly;
                case SoundCue.Enemy:
                    return enemy;
                case SoundCue.BattleIntro:
                    return battleIntro;
                case SoundCue.Victory:
                    return victory;
                case SoundCue.Defeat:
                    return defeat;
                case SoundCue.LevelUp:
                    return levelUp;
                case SoundCue.Gold:
                    return gold;
                case SoundCue.Equip:
                    return equip;
                case SoundCue.Click:
                    return click;
                case SoundCue.Hit:
                    return hit;
                case SoundCue.HeavyHit:
                    return heavyHit;
                case SoundCue.Hurt:
                    return hurt;
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
                case MusicLayer.Battle:
                    return battleLayer;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layer), layer, "No sound field for this layer.");
            }
        }
    }
}
