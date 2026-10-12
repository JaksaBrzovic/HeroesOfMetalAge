using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Infrastructure.Content;
using MLin.Audio;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Audio
{
    /// <summary>
    /// Plays the game's audio through the MLin audio system. It listens to gameplay facts published by
    /// <see cref="GameSession"/> after commands succeed and never decides gameplay. The ambient, gallop and battle layers
    /// start together and are mixed by travel state; event sounds come from <see cref="AudioDatabaseScriptableObject"/>
    /// and unit voices from the unit database.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour, IAudioControls
    {
        public const string MASTER_CHANNEL = "Master";
        public const string MUSIC_CHANNEL = "Music";
        public const string EFFECTS_CHANNEL = "Effects";
        public const string VOICE_CHANNEL = "Voice";

        private const float VOICE_REPLACE_FADE = 0.05f;
        private const float PREVIEW_STOP_FADE = 0.3f;
        private const float MAX_PREVIEW_SECONDS = 10f;

        private readonly Dictionary<string, SoundDefinition> unitVoices = new Dictionary<string, SoundDefinition>(StringComparer.Ordinal);
        private AudioDatabaseScriptableObject database;
        private GameSession session;
        private SoundHandle[] layers = Array.Empty<SoundHandle>();
        private SoundHandle voice;
        private SoundHandle preview;
        private float previewStopTime;
        private bool isMuted;
        private bool isInitialized;

        public float MusicVolume
        {
            get => AudioSystem.GetChannelVolume(MUSIC_CHANNEL);
            set => AudioSystem.SetChannelVolume(MUSIC_CHANNEL, value);
        }

        public float EffectsVolume
        {
            get => AudioSystem.GetChannelVolume(EFFECTS_CHANNEL);
            set => AudioSystem.SetChannelVolume(EFFECTS_CHANNEL, value);
        }

        public float VoiceVolume
        {
            get => AudioSystem.GetChannelVolume(VOICE_CHANNEL);
            set => AudioSystem.SetChannelVolume(VOICE_CHANNEL, value);
        }

        /// <summary>Mutes through the Master channel; not persisted, every session starts unmuted.</summary>
        public bool IsMuted
        {
            get => isMuted;
            set
            {
                isMuted = value;
                AudioSystem.SetChannelVolume(MASTER_CHANNEL, value ? 0f : 1f);
            }
        }

        public bool IsRiding { get; private set; }
        public bool IsFighting { get; private set; }

        /// <exception cref="InvalidOperationException">Called a second time.</exception>
        public void Initialize(AudioDatabaseScriptableObject audioDatabase, UnitDatabaseScriptableObject unitDatabase, GameSession gameSession)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Game audio was initialized twice.");
            }

            database = audioDatabase;
            session = gameSession;
            foreach (UnitEntry unit in unitDatabase.Units)
            {
                if (unit.Voice != null)
                {
                    unitVoices.Add(unit.Id, unit.Voice);
                }
            }

            AudioSystem.Initialize(database.Settings);
            StartLayers();

            session.CueRequested += HandleCueRequested;
            session.UnitVoiceRequested += HandleUnitVoiceRequested;
            session.PlaybackChanged += HandlePlaybackChanged;
            session.DamageResolved += HandleDamageResolved;
            isInitialized = true;
        }

        public void Audition(SoundCue cue)
        {
            AudioSystem.Play(database.Sound(cue));
        }

        public void AuditionLayer(MusicLayer layer)
        {
            SoundDefinition sound = database.Layer(layer);
            preview.Stop();
            preview = AudioSystem.Play(sound);
            previewStopTime = Time.unscaledTime + PreviewSeconds(sound);
        }

        public void AuditionVoice(string unitId)
        {
            PlayVoice(VoiceFor(unitId));
        }

        public void SavePreferences()
        {
            AudioSystem.SaveVolumes();
        }

        /// <returns>The unit's voice as loaded at initialization, or null for a silent unit.</returns>
        internal SoundDefinition VoiceFor(string unitId)
        {
            return unitVoices.TryGetValue(unitId, out SoundDefinition sound) ? sound : null;
        }

        private void Update()
        {
            if (preview.IsPlaying && Time.unscaledTime >= previewStopTime)
            {
                preview.Stop(PREVIEW_STOP_FADE);
            }
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.CueRequested -= HandleCueRequested;
                session.UnitVoiceRequested -= HandleUnitVoiceRequested;
                session.PlaybackChanged -= HandlePlaybackChanged;
                session.DamageResolved -= HandleDamageResolved;
            }

            // the voices belong to this adapter; the authored clips and definitions are shared assets and stay untouched
            foreach (SoundHandle layer in layers)
            {
                layer.Stop();
            }

            preview.Stop();
            voice.Stop();
        }

        private void HandleCueRequested(SoundCue cue)
        {
            Audition(cue);
        }

        private void HandleUnitVoiceRequested(string unitId)
        {
            PlayVoice(VoiceFor(unitId));
        }

        private void HandlePlaybackChanged(PlaybackState state)
        {
            IsRiding = state.IsRiding;
            IsFighting = state.IsFighting;
            ApplyLayerMix(database.LayerFadeSeconds);
        }

        private void HandleDamageResolved(DamageEvent hit)
        {
            SoundCue cue;
            if (hit.IsReceivedByPlayer)
            {
                cue = SoundCue.Hurt;
            }
            else
            {
                cue = hit.Damage >= database.HeavyHitThreshold ? SoundCue.HeavyHit : SoundCue.Hit;
            }

            Audition(cue);
        }

        // voices replace each other so a stack's call is never buried under the previous one
        private void PlayVoice(SoundDefinition sound)
        {
            if (sound == null)
            {
                return;
            }

            voice.Stop(VOICE_REPLACE_FADE);
            voice = AudioSystem.Play(sound);
        }

        private void StartLayers()
        {
            MusicLayer[] order = (MusicLayer[])Enum.GetValues(typeof(MusicLayer));
            SoundDefinition[] sounds = new SoundDefinition[order.Length];
            for (int i = 0; i < order.Length; i++)
            {
                sounds[i] = database.Layer(order[i]);
            }

            // all layers begin on one DSP time; the mix is set before the first sample plays
            layers = AudioSystem.PlayLayers(sounds);
            ApplyLayerMix(0f);
        }

        private void ApplyLayerMix(float fade)
        {
            // indexed by MusicLayer; empty until the layers start
            if (layers.Length == 0)
            {
                return;
            }

            float ambient = IsFighting ? database.AmbientInBattle : IsRiding ? database.AmbientWhileRiding : database.AmbientAtRest;
            float gallop = IsRiding && !IsFighting ? database.GallopMix : 0f;
            float battle = IsFighting ? database.BattleMix : 0f;
            layers[(int)MusicLayer.Ambient].SetVolume(ambient, fade);
            layers[(int)MusicLayer.Gallop].SetVolume(gallop, fade);
            layers[(int)MusicLayer.Battle].SetVolume(battle, fade);
        }

        private static float PreviewSeconds(SoundDefinition sound)
        {
            foreach (AudioClip clip in sound.Clips)
            {
                if (clip != null)
                {
                    return Mathf.Min(clip.length, MAX_PREVIEW_SECONDS);
                }
            }

            return 0f;
        }
    }
}
