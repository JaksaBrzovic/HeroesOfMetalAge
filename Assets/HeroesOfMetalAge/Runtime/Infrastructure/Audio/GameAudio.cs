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
    /// <see cref="GameSession"/> after commands succeed and never decides gameplay. One music track leads at a time: the
    /// mission's map music (quiet while the hero stands, full while he rides) or town music, the battle music, the victory
    /// music after the victory stinger for as long as the victory and rewards screens are open, or the defeat theme. The ambient and gallop layers start together and
    /// lead only when no track does (a mission without map music); the riding loop follows the ride, strikes play their swing and impact in sequence, event sounds come from
    /// <see cref="AudioDatabaseScriptableObject"/> and unit voices from the unit database. The main menu has the map
    /// music; after New Game the music and the layers fade out with the screen, stay silent on black and during the intro
    /// (which plays the end-of-day sound) and fade in with the mission.
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
        // strike sounds waiting for their time, in the order they were scheduled
        private readonly List<ScheduledCue> scheduledCues = new List<ScheduledCue>();
        private AudioDatabaseScriptableObject database;
        private MissionSettingsScriptableObject mission;
        private GameSession session;
        private FrontEndFlow frontEnd;
        private SoundHandle[] layers = Array.Empty<SoundHandle>();
        private SoundHandle voice;
        private SoundHandle preview;
        private SoundHandle riding;
        private SoundHandle music;
        private MusicTrack? currentTrack;
        private float previewStopTime;
        private float nextStrikeTime;
        // the victory music starts at this time (unscaled), after the stinger
        private float victoryStartTime;
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
        public bool IsDefeated { get; private set; }
        public bool IsInTown { get; private set; }
        public bool IsCelebrating { get; private set; }
        /// <summary>True from New Game until the intro's title: no music and no layers.</summary>
        public bool IsSilenced { get; private set; }
        /// <summary>The music track playing now, or null while the layers lead.</summary>
        public MusicTrack? CurrentTrack => currentTrack;

        /// <exception cref="InvalidOperationException">Called a second time.</exception>
        public void Initialize(AudioDatabaseScriptableObject audioDatabase, UnitDatabaseScriptableObject unitDatabase,
            MissionSettingsScriptableObject missionSettings, GameSession gameSession, FrontEndFlow frontEndFlow)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("Game audio was initialized twice.");
            }

            database = audioDatabase;
            mission = missionSettings;
            session = gameSession;
            frontEnd = frontEndFlow;
            IsSilenced = IsSilentPhase(frontEnd.Phase);
            foreach (UnitEntry unit in unitDatabase.Units)
            {
                if (unit.Voice != null)
                {
                    unitVoices.Add(unit.Id, unit.Voice);
                }
            }

            AudioSystem.Initialize(database.Settings);
            StartLayers();
            ApplyState(session.Playback, true);

            session.CueRequested += HandleCueRequested;
            session.UnitVoiceRequested += HandleUnitVoiceRequested;
            session.PlaybackChanged += HandlePlaybackChanged;
            session.DamageResolved += HandleDamageResolved;
            frontEnd.PhaseChanged += HandleFrontEndPhaseChanged;
            isInitialized = true;
        }

        public void Play(SoundCue cue)
        {
            AudioSystem.Play(database.Sound(cue));
        }

        public void Audition(SoundCue cue)
        {
            Play(cue);
        }

        public void AuditionLayer(MusicLayer layer)
        {
            Preview(database.Layer(layer));
        }

        public void AuditionMusic(MusicTrack track)
        {
            SoundDefinition sound = MusicFor(track);
            if (sound != null)
            {
                Preview(sound);
            }
        }

        public void AuditionRidingLoop()
        {
            Preview(database.RidingLoop);
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

        /// <summary>The cues a strike plays, in order: the swing or release, then the hit or miss, then a heavy hit if it was one.</summary>
        internal IReadOnlyList<SoundCue> StrikeCues(DamageEvent hit)
        {
            List<SoundCue> cues = new List<SoundCue>(3);
            if (hit.IsRanged)
            {
                cues.Add(SoundCue.ArrowFire);
                cues.Add(hit.IsMiss ? SoundCue.ArrowMiss : SoundCue.ArrowHit);
            }
            else
            {
                cues.Add(SoundCue.SwordSwing);
                cues.Add(hit.IsMiss ? SoundCue.SwordMiss : SoundCue.SwordHit);
            }

            if (!hit.IsMiss && hit.Damage >= database.HeavyHitThreshold)
            {
                cues.Add(SoundCue.HeavyHit);
            }

            return cues;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (preview.IsPlaying && now >= previewStopTime)
            {
                preview.Stop(PREVIEW_STOP_FADE);
            }

            for (int i = 0; i < scheduledCues.Count; i++)
            {
                if (scheduledCues[i].Time <= now)
                {
                    Audition(scheduledCues[i].Cue);
                    scheduledCues.RemoveAt(i);
                    i--;
                }
            }

            // the victory music starts on its own schedule, after the stinger
            if (isInitialized)
            {
                ApplyMusic(false);
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

            if (frontEnd != null)
            {
                frontEnd.PhaseChanged -= HandleFrontEndPhaseChanged;
            }

            // the voices belong to this adapter; the authored clips and definitions are shared assets and stay untouched
            foreach (SoundHandle layer in layers)
            {
                layer.Stop();
            }

            scheduledCues.Clear();
            preview.Stop();
            voice.Stop();
            riding.Stop();
            if (currentTrack.HasValue)
            {
                AudioSystem.StopMusic(0f);
            }
        }

        private void HandleCueRequested(SoundCue cue)
        {
            Audition(cue);
            if (cue == SoundCue.Victory)
            {
                QueueVictoryMusic();
            }
        }

        // out with the fade to black, back with a long fade as the intro's title appears
        private void HandleFrontEndPhaseChanged(FrontEndPhase phase)
        {
            bool isSilenced = IsSilentPhase(phase);
            if (isSilenced == IsSilenced)
            {
                return;
            }

            IsSilenced = isSilenced;
            float fade = isSilenced ? frontEnd.FadeOutSeconds : frontEnd.MusicFadeInSeconds;
            ApplyMusic(false, fade);
            ApplyLayerMix(fade);
            ApplyRiding();
        }

        private void HandleUnitVoiceRequested(string unitId)
        {
            PlayVoice(VoiceFor(unitId));
        }

        private void HandlePlaybackChanged(PlaybackState state)
        {
            ApplyState(state, false);
        }

        // the swing starts at once (or after the previous strike of the same action), the impact follows it
        private void HandleDamageResolved(DamageEvent hit)
        {
            float start = Mathf.Max(Time.unscaledTime, nextStrikeTime);
            float impact = start + (hit.IsRanged ? database.RangedImpactSeconds : database.MeleeImpactSeconds);
            IReadOnlyList<SoundCue> cues = StrikeCues(hit);
            scheduledCues.Add(new ScheduledCue(start, cues[0]));
            for (int i = 1; i < cues.Count; i++)
            {
                scheduledCues.Add(new ScheduledCue(impact, cues[i]));
            }

            nextStrikeTime = start + database.StrikeSpacingSeconds;
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

        private void ApplyState(PlaybackState state, bool isInstant)
        {
            IsRiding = state.IsRiding;
            IsFighting = state.IsFighting;
            IsDefeated = state.IsDefeated;
            IsInTown = state.IsInTown;
            IsCelebrating = state.IsCelebrating;
            ApplyMusic(isInstant);
            ApplyLayerMix(isInstant ? 0f : database.LayerFadeSeconds);
            ApplyMapMusicLevel(isInstant ? 0f : database.MapMusicRideFadeSeconds);
            ApplyRiding();
        }

        // the victory music waits for the stinger; it loops until the rewards screen closes, and its fade-out overlaps the
        // returning map or town music
        private void QueueVictoryMusic()
        {
            victoryStartTime = Time.unscaledTime + database.VictoryMusicDelaySeconds;
        }

        private SoundDefinition MusicFor(MusicTrack track)
        {
            switch (track)
            {
                case MusicTrack.Map:
                    return mission.MapMusic;
                case MusicTrack.Town:
                    return mission.TownMusic;
                default:
                    return database.Music(track);
            }
        }

        private MusicTrack? WantedTrack()
        {
            if (IsSilenced)
            {
                return null;
            }

            if (IsFighting)
            {
                return MusicTrack.Battle;
            }

            if (IsDefeated)
            {
                return MusicTrack.Defeat;
            }

            if (IsCelebrating)
            {
                // silence under the stinger, then the victory music
                return Time.unscaledTime >= victoryStartTime ? MusicTrack.Victory : (MusicTrack?)null;
            }

            if (IsInTown && mission.TownMusic != null)
            {
                return MusicTrack.Town;
            }

            // a map music without its clip (a local file missing from this copy) leaves the map to the layers
            return mission.MapMusic != null && mission.MapMusic.HasClips ? MusicTrack.Map : (MusicTrack?)null;
        }

        private void Preview(SoundDefinition sound)
        {
            preview.Stop();
            preview = AudioSystem.Play(sound);
            previewStopTime = Time.unscaledTime + PreviewSeconds(sound);
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

            if (IsSilenced)
            {
                layers[(int)MusicLayer.Ambient].SetVolume(0f, fade);
                layers[(int)MusicLayer.Gallop].SetVolume(0f, fade);
                return;
            }

            bool isMusicLeading = currentTrack.HasValue;
            float ambient = isMusicLeading ? database.AmbientUnderMusic : IsRiding ? database.AmbientWhileRiding : database.AmbientAtRest;
            float gallop = IsRiding && !isMusicLeading ? database.GallopMix : 0f;
            layers[(int)MusicLayer.Ambient].SetVolume(ambient, fade);
            layers[(int)MusicLayer.Gallop].SetVolume(gallop, fade);
        }

        // battle, victory and defeat music start from the beginning; map and town music continue where they stopped when
        // their SoundDefinition resumes its position
        private void ApplyMusic(bool isInstant, float frontEndFade = -1f)
        {
            MusicTrack? wanted = WantedTrack();
            if (wanted == currentTrack)
            {
                return;
            }

            float fade = isInstant ? 0f
                : frontEndFade >= 0f ? frontEndFade
                : wanted == MusicTrack.Victory ? database.VictoryMusicFadeInSeconds
                : currentTrack == MusicTrack.Victory ? database.VictoryMusicFadeOutSeconds
                : database.LayerFadeSeconds;
            currentTrack = wanted;
            if (wanted.HasValue)
            {
                bool isRestarting = wanted.Value != MusicTrack.Map && wanted.Value != MusicTrack.Town;
                music = AudioSystem.PlayMusic(MusicFor(wanted.Value), fade, isRestarting);
                ApplyMapMusicLevel(0f);
            }
            else
            {
                AudioSystem.StopMusic(fade);
                music = SoundHandle.Invalid;
            }

            ApplyLayerMix(isInstant ? 0f : frontEndFade >= 0f ? frontEndFade : database.LayerFadeSeconds);
        }

        // on top of its crossfade, the map music sits low while the hero stands and rises while he rides
        private void ApplyMapMusicLevel(float fade)
        {
            if (currentTrack == MusicTrack.Map)
            {
                music.SetVolume(IsRiding ? 1f : database.MapMusicIdleLevel, fade);
            }
        }

        private void ApplyRiding()
        {
            if (IsRiding && !IsSilenced && !riding.IsPlaying)
            {
                riding = AudioSystem.Play(database.RidingLoop, database.RidingFadeSeconds);
            }
            else if (!IsRiding || IsSilenced)
            {
                riding.Stop(database.RidingFadeSeconds);
            }
        }

        private static bool IsSilentPhase(FrontEndPhase phase)
        {
            return phase == FrontEndPhase.FadingOut || phase == FrontEndPhase.Black || phase == FrontEndPhase.Verses;
        }

        private static float PreviewSeconds(SoundDefinition sound)
        {
            float seconds = sound.IntroClip != null ? sound.IntroClip.length : 0f;
            foreach (AudioClip clip in sound.Clips)
            {
                if (clip != null)
                {
                    return Mathf.Min(seconds + clip.length, MAX_PREVIEW_SECONDS);
                }
            }

            return 0f;
        }

        private readonly struct ScheduledCue
        {
            public ScheduledCue(float time, SoundCue cue)
            {
                Time = time;
                Cue = cue;
            }

            public float Time { get; }
            public SoundCue Cue { get; }
        }
    }
}
