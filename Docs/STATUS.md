# Prototype status

Playable prototype 0.4 on Unity 6000.3.26f1. Runtime is split into five dependency-enforced runtime assemblies (Domain, Application, Presentation, Infrastructure, Bootstrap) plus Editor tooling and an EditMode test assembly.

Delivered: adventure map and route arrows, day/movement economy, joining encounter, blocking encounter, hex combat with simple enemy AI, town recruitment, weekly stock growth, ownership through combat, protected clan home army, owned home/visiting army transfer, hero home/visiting placement, gold/XP/reward artefacts, equipment/inventory, level bonuses, victory/defeat/retry, save/load, sound-state layers, distinct hits and creature calls, audio options and the F3 development panel.

Audio runs on the MLin Audio System package (v0.2.0): every cue is a `SoundDefinition`, the mixer has Master/Music/Effects/Voice channels, music ducks under unit voices, the three music layers start sample-aligned and all audio mutes while the window is unfocused.

Data-driven authoring is implemented through AudioDatabaseScriptableObject, UnitDatabaseScriptableObject, ArtefactDatabaseScriptableObject, HeroDataBaseScriptableObject and MissionSettingsScriptableObject. Gameplay uses immutable definitions and encapsulated state; views dispatch commands, audio consumes typed facts and persistence migrates version 1 saves to stable-ID version 2 snapshots.

The whole UI, including the F3 development panel, is prefab-authored Canvas/uGUI with TextMeshPro: 20 saved UI prefabs cover the root, gameplay screens, dialogs, options, header/footer/notifications, the development panel and nine shared widgets. Views reference their controls through typed serialized fields. Prefabs are authored assets; only the bootstrap scene is generated.

Verification: **EditMode tests** (`Tests/EditMode`, run by `Tools/Build.ps1`) cover campaign rules and atomic rejections, battle resolution and replay, routing, persistence and save migration, content validation and authored-data consumption, audio cue resolution and routing, and the UI prefabs (initialization against real content, canvas/input setup, fonts, raycasts, nested widgets, the development panel, sprite fallbacks and the absence of script-built UI). The **built-player scenario** (`Tools/SmokeTest.ps1`) plays the whole mission including conquest, home/visiting, save/load, victory, defeat and retry, checks pointer raycasts and resolutions, and captures screenshots that are inspected. Latest results are in `Reports/` (not committed).

The visuals and synthetic audio are placeholders. Recorded guitar stems and final voice performances remain a creative production milestone. The prototype does not include town construction/investment, negotiations, spells, multiple heroes, partial-stack splitting, multiplayer or campaigns beyond this mission.

Next useful iteration: user playthrough and listening feedback, especially gallop timing, perceived danger, impact intensity, the voice ducking amount, combat pacing and recruitment usability. Then replace placeholder audio and art while preserving the working mission loop.
