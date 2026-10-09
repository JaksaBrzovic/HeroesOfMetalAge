# Heroes of Metal Age

Unity 6000.3.26f1. Windows standalone 2D strategy prototype. Preserve the player's sound-first design and work through CLI and Editor scripts; no manual scene wiring should be required.

Read Docs/PROTOTYPE.md and Docs/AUDIO_DIRECTION.md before substantial changes. Runtime rules live in MetalRules.cs, UI/campaign/combat in MetalGame.cs, audio in MetalAudio.cs. The current immediate-mode UI and procedural art are prototype implementations, not the final visual target.

Use Tools/Build.ps1 to generate the scene, validate rules and build. Use Tools/SmokeTest.ps1 to run a complete mission simulation and capture screens. Inspect build/player logs for exceptions. Keep meaningful rules checks in PrototypePipeline.Validate. Test changes at the appropriate scope.

PrototypePipeline.Setup regenerates only Assets/Scenes/AshenPass.unity. Do not place authored content in that generated scene. Keep new reusable authored assets elsewhere. Preserve Unity .meta GUIDs. Do not upgrade Unity or introduce third-party services without a concrete project need.

Keep campaign economy and combat deterministic where practicable. Never generate audio at the moment an attack occurs; clips should be prepared before playback. Runtime smoke tests must use isolated save data and must not overwrite the player's campaign.
