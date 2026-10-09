# Sound-first direction

Original dark heavy-metal atmosphere inspired by the user's references to Manowar and Bathory. Do not use their recordings or reproduce melodies. Prototype audio is intentionally synthesized and must be described as placeholder audio.

MetalAudio is the central director. Ambient, gallop and combat loops start at the same DSP time and use 100 BPM / 9.6-second loops. Layer volumes fade with state. Friendly/enemy encounter, battle intro, victory, defeat and level use short riffs. Small/heavy/received hits have different sound events; gold/equipment/click use shorter feedback. Creature calls are loaded from Resources/Voices.

User has independent music, effects and voice controls plus mute. F3 auditions every event without replaying the mission. Long-lived layer playback avoids restarting ambience on each action. Effect priorities and the short hit throttle prevent unbounded stacking.

Next audio milestone: replace synthesis with authored looping stems of matching duration/tempo/key; add alternative impact/encounter riffs, beat-aware transitions, voice performance and ducking. Keep input/impact responses prompt even when musical changes wait for a beat. Evaluate fatigue over a full playthrough, not just single-event auditions.
