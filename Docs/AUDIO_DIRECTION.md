# Sound-first direction

Original dark heavy-metal atmosphere inspired by the user's references to Manowar and Bathory. Do not use their recordings or reproduce melodies. Prototype audio is intentionally synthesized and must be described as placeholder audio.

Playback runs on the MLin Audio System package. `GameAudio` in `Runtime/Infrastructure/Audio` is the only adapter: it receives typed successful-gameplay facts from `GameSession` and never determines gameplay. Every sound is a `SoundDefinition` asset (`Content/Audio/Sounds/`) that owns the clips and variants, volume, pitch, priority, instance limit, cooldown and mixer group. `AudioDatabaseScriptableObject` has one field per game event (`Application/SoundCue` enum) and per music layer, plus the layer mix and the heavy-hit threshold. Each unit references its own voice in the unit database.

Default ambient, gallop and combat loops are 100 BPM / 9.6-second WAV files under `Content/Audio/`. They start sample-aligned on one DSP time (`AudioSystem.PlayLayers`) and their volumes fade with travel and combat state. Friendly/enemy encounter, battle intro, victory, defeat and level use short riffs. Small/heavy/received hits have different sound events; gold/equipment/click use shorter feedback. Unit voices replace each other instead of overlapping.

Mix: `GameMixer` routes Music, Effects and Voice under Master. Players have independent music, effects and voice volumes (saved by the audio system's built-in PlayerPrefs storage) plus mute; all audio mutes while the window is unfocused. Music ducks 6 dB while a unit speaks (`GameAudioSettings` ducking rule). Instance limits and priorities on the SoundDefinitions prevent unbounded stacking; music layers are never stolen by effects. F3 opens the uGUI development panel that auditions every layer, event sound and unit voice without replaying the mission.

Database and SoundDefinition edits take effect on a new Play Mode initialization or a rebuilt player. New Mission resets campaign state without reloading the catalog.

Next audio milestone: replace synthesis with authored looping stems of matching duration/tempo/key; add alternative impact/encounter riffs and voice performance. When distinct tracks arrive, switch them with `PlayMusic(..., MusicSync.NextBar)` so changes land on the beat while input and impact responses stay immediate. Evaluate fatigue over a full playthrough, not just single-event auditions.
