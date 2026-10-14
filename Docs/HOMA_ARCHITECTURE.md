# Heroes of Metal Age — architecture

Status: prototype 0.7, Unity 6000.3.26f1, Windows, Universal Render Pipeline (2D Renderer), target resolution Full HD (1920x1080, 16:9). This document records only what is specific to this project: module map, ownership, contracts and decisions. General Unity coding and architecture conventions (naming, formatting, lifecycle, uGUI layout/input/performance, SOLID, testing levels) come from the shared Unity guidance and are not repeated here. Read together with `PROTOTYPE.md` and `AUDIO_DIRECTION.md`.

## 1. Project requirements

- **Content:** all designer-editable content and tuning live in ScriptableObject assets, so stats, text, images and sounds change in the Inspector without code. Runtime state and saves stay separate.
- **UI:** the whole UI, including the F3 development panel, is prefab-authored uGUI with TextMeshPro. No runtime code uses IMGUI or builds controls. The design resolution is 1920x1080, so at the target one UI unit and one map image pixel are one screen pixel.
- **World:** the adventure map and the battlefield are world objects (sprites, a Tilemap logic layer, an orthographic camera) drawn inside viewports the UI leaves open. UI is UI; everything else is world.
- **Scope:** sound-first, turn-based, single player. Multiplayer, ECS, network determinism and modding are not current requirements.

## 2. Module map

Paths are relative to `Assets/HeroesOfMetalAge/`. Each row is an assembly definition.

| Assembly | Contents | Known limits |
|---|---|---|
| `Runtime/Domain` (no engine refs) | Definitions and coordinates, `CampaignState`, `CampaignRules`, `WorldMap` pathfinding, `TerrainRules` (passability, movement costs, diagonal cost), `HexGrid`, `BattleEngine`, `DamageCalculator`, `BattleAi`, `CampaignSnapshot` | One hero, one town, one encounter of each kind |
| `Runtime/Application` (no engine refs) | `GameSession` (commands, transitions, revisions, typed events), `SoundCue`/`MusicLayer` enums, ports `ISaveStore` and `IAudioControls` | Synchronous commands, no networking |
| `Runtime/Presentation` | UI: `GameHud` router, one `CanvasScreenView` per screen, header/footer/notifications, `DebugPanelView`, `UiWidget`, `UiTheme`, `CanvasViewContext`. World: `GameWorld`, `WorldCamera`, `AdventureMapView`, `BattlefieldView`, `WorldMarker`, `FloatingText` | Placeholder art, no zoom |
| `Runtime/Infrastructure` | Content databases, `AdventureMapAuthoring` and `TerrainTile` (map image and logic layer), `ContentValidator`, `ContentLoader`; `GameAudio`; `JsonCampaignStore` | No live content refresh or streaming |
| `Runtime/Dialogue` | Self-contained dialogue feature: `DialogueDatabaseScriptableObject` (speakers, conversations with a `DialogueStyle`, lines, typing speed), `DialogueDirector` (public API), `DialogueView` base with `DialogueBoxView` (portrait box) and `DialogueBalloonView` (comic speech balloon), `DialogueTypewriter`, `IDialogueVoice` and its MLin adapter `AudioSystemDialogueVoice` | Fixed-size box and cloud, click to advance, single-language text |
| `Runtime/Bootstrap` | `GameBootstrap` composition root, `SiteDialogueCues` (plays the mission sites' conversations), development-only `RuntimeSmokeRunner` | — |
| `Editor` | `ContentAssets` locator, `PrototypePipeline` (scene generation, build), `DialogueAssetSeeder` (one-off: created the initial dialogue database and prefabs, now only recreates missing ones; can be deleted with its call in `PrototypePipeline.Setup`), `AdventureMapBuilder` and its Inspector (Rebuild), `MapCellHoverLabel` (cell coordinates in the Scene view) | — |
| `Tests/EditMode` | Rules, battle/world, persistence, content, audio routing, dialogue, UI prefabs, world prefabs, logic layer, cell and hex picking, large-map routing | No PlayMode tests yet |

```text
Presentation ----> Application ----> Domain
Infrastructure --> Application ports / Domain
Dialogue --------> MLin.Audio, uGUI, TextMeshPro (no game assembly)
Bootstrap -------> everything (composition only)
Editor, Tests ---> runtime assemblies
```

The map prefab carries both `AdventureMapAuthoring` (Infrastructure: what the rules read) and `AdventureMapView` (Presentation: what is drawn); Bootstrap instantiates it, so neither assembly references the other. Infrastructure references the `MLin.Audio` package assembly; `AssemblyInfo.cs` exposes internals to Editor and Tests. Presentation and Infrastructure never reference each other. No game assembly references `Dialogue` except Bootstrap, Editor and Tests; game code that wants a dialogue gets the `DialogueDirector` from the composition root and passes a conversation ID. Namespaces follow the layer (`HeroesOfMetalAge.Domain`, `.Application`, ...); split by feature only when a layer grows.

## 3. State and data ownership

| Data | Owner | Examples |
|---|---|---|
| Authored definitions | ScriptableObject databases → read-only `DefinitionCatalog` | Unit stats, artefact bonuses, mission and map logic layer, cue mapping |
| Runtime state | `GameSession` → `CampaignState`, `BattleEngine` | Gold, armies, day, hero position, town owner, battle HP |
| Save format | `CampaignSnapshot` (DTO) via `ISaveStore` | Schema version, stable IDs |

- One `GameSession` owns the campaign and at most one battle. Domain operations own every write; views get read-only state and audio gets typed events.
- `ContentLoader` validates the databases (`ContentValidator`) and projects them into plain definitions once at startup. Domain never sees ScriptableObjects; Presentation and `GameAudio` keep the Sprite/SoundDefinition references.
- Sounds are referenced directly, never by string ID. A unit entry holds its own voice `SoundDefinition`; the AudioDatabase holds one field per `SoundCue` and `MusicLayer`. Gameplay publishes the enum value or the unit ID, and `GameAudio` resolves the sound.
- Asset edits apply on the next Play Mode start or build. New Mission resets the campaign against the loaded catalog without reloading it.

### Content assets (`Content/`)

| Asset | Class | Holds |
|---|---|---|
| `Databases/UnitDatabase.asset` | `UnitDatabaseScriptableObject` | ID, name, description, portrait, walk cycle, health, damage range, attack/defense, speed, ranged, price, weekly growth, voice (`SoundDefinition`) |
| `Databases/ArtefactDatabase.asset` | `ArtefactDatabaseScriptableObject` | ID, name, description, icon, slot, attack/defense/speed bonus |
| `Databases/HeroDatabase.asset` | `HeroDataBaseScriptableObject` | ID, name, portrait, map ride cycle, faction/class, base stats, movement, progression, starting army |
| `Databases/AudioDatabase.asset` | `AudioDatabaseScriptableObject` | Audio system settings; a `SoundDefinition` field per music layer and per `SoundCue`; layer mix, layer fade time, heavy-hit threshold |
| `Databases/AshenPass.asset` | `MissionSettingsScriptableObject` | Map prefab, terrain database, start and encounter cells (from the top-left), encounters with rewards and battlefield images, gold, town/faction, prices, default battlefield image, combat tuning, pacing |
| `Databases/TerrainDatabase.asset` | `TerrainDatabaseScriptableObject` | Per terrain: passable or not and movement cost (road 75, grass/dirt 100, rough 125, sand 150, swamp 175; forest, mountain, water, lava, obstacle impassable); diagonal step cost 141 % |
| `Units/*`, `Heroes/*`, `World/Battlefields/*` | Sprites | Walk cycles (Roadie, Air Guitarist), the hero's ride cycle and the green and infernal battlefield images |
| `Maps/AshenPass/AshenPassMap.prefab`, `Maps/AshenPass/AshenPass.png` | `AdventureMapAuthoring` + `AdventureMapView` | Map image, pixels per cell, the painted terrain logic layer, display size |
| `Maps/TerrainLogic/*.asset`, `Terrain Logic.prefab` | `TerrainTile`, Tile Palette | Grass, Road, Forest, Water, Mountain logic tiles and their palette |
| `Rendering/UniversalRenderPipeline.asset`, `WorldRenderer2D.asset` | URP asset, 2D Renderer | Pipeline settings; sprites sort by Y (lower is in front) |
| `Audio/GameAudioSettings.asset`, `Audio/GameMixer.mixer`, `Audio/Sounds/*.asset` | MLin settings, mixer, `SoundDefinition` | Channels, ducking rule, focus muting; one sound per cue |
| `UI/UiTheme.asset` | `UiTheme` | TMP font, placeholder sprites, palette and hex colours |
| `Databases/DialogueDatabase.asset` | `DialogueDatabaseScriptableObject` | Typing speed; speakers (ID, name, portrait); conversations (ID, lines: ID, speaker ID, text, optional voice-over `SoundDefinition`) |

The database class names (including `HeroDataBaseScriptableObject`) are fixed by user request. Every content asset is authored, never regenerated (a map Rebuild only re-imports its image and trims or resamples its logic layer); only `Assets/Scenes/AshenPass.unity` is generated (Tools > Heroes of Metal Age > Generate scene).

## 4. Commands and events

Views call `GameSession` methods: `Plan`, `Travel`, `EndDay`, `AcceptFriendly`, `FightEncounter`, `BattleAction`, `ContinueVictory`, `PrepareAfterDefeat`, `Buy`, `Transfer`, `StationHero`, `ClaimTown`, `Equip`, `Unequip`, `Save`, `Load`, `Restart` (plus `Debug*` for the development panel). Each command validates the whole outcome first, applies it atomically through `CampaignRules`/`BattleEngine` and returns or publishes a `CommandResult`. A disabled button is a convenience, never the validation.

`GameSession.Revision` increments on every state change; `GameHud` redraws only when it changes and widgets skip unchanged values. Audio receives `CueRequested` (`SoundCue`), `UnitVoiceRequested` (unit ID), `PlaybackChanged` and `DamageResolved` after a command succeeds, and dialogue cues receive `SiteReached`; a listener failure never rolls back gameplay. `GameSession.IsBusy` is the single transition guard against overlapping travel, town and combat actions.

## 5. Game rules the architecture must preserve

- Adventure grid (`WorldPosition`) and combat hexes (`HexPosition`) are separate value types. `WorldPosition` rows count from the top; Tilemap rows count from the bottom, converted only in `AdventureMapAuthoring.ReadTerrain` and `AdventureMapView`. Unpainted cells are `TerrainType.Blocked`. Pathfinding (a binary-heap Dijkstra, fast on maps of hundreds of cells per side) allows diagonals without corner cutting; among equally cheap routes it prefers the shortest on the map (diagonal ≈ 1.4 straight steps), then the one closest to the straight line, then row and column; travel stops before an unresolved encounter or when movement runs out.
- Movement points are 1600 per day. A step costs the destination's terrain cost from `TerrainRules`, times 1.41 when diagonal; travel stops when the next step is not affordable.
- `EndDay` restores movement and applies weekly growth on days 8, 15, 22…; loading a save never applies growth again.
- Combat damage, retaliation (once per round), ranged half-damage in melee, defending (−30 %) and casualties are resolved by `BattleEngine` independently of animation. Randomness comes from an injected `IRandomSource`; tests use fixed seeds. `BattleAi` acts through the same validated actions as the player.
- Victory rewards are granted once, even if the terminal action repeats. Defeat offers a retry from the pre-battle snapshot without persistent casualties.
- Town ownership is explicit. The neutral clan garrison cannot be transferred; owning the town unlocks transfers and 55 % recruit prices. Home and visiting heroes are distinct; stationing merges armies only after the full destination is validated.
- Hero stats are base + level + equipped bonuses, always computed, never written back into base stats.

## 6. UI and world composition

**UI.** `GameUiRoot.prefab` (under `Prefabs/UI/`) owns the CanvasScaler (1920×1080 design frame, Expand, letterboxed), the single EventSystem (legacy input module) and nested screen prefabs: `Adventure`, `Combat`, `Town`, `Hero`, `Dialog`, `Options`, `Header`, `Footer`, `Notifications`. `DebugPanel.prefab` is instantiated under the design frame only in the Editor and development builds. `DialogueOverlay.prefab` is a separate Screen Space Overlay Canvas (sorting order 10, the same 1920x1080 Expand scaler and a `DesignFrame`) holding the nested `Widgets/DialogueBox.prefab`; its own Canvas keeps the per-frame typing out of the game UI's rebuilds, and it draws above the game UI including options and the F3 panel. The box is not modal: only clicks on the box itself are taken (they also stop world clicks through the EventSystem raycast), so gameplay continues around it. Four shared widgets live in `Widgets/`. Each view binds its controls through named, typed serialized fields (`titleText`, `rideButton`, `portraitImage`) and reports a missing reference by field name on startup. `Adventure` and `Combat` leave a `MapViewport` / `BattleViewport` RectTransform with no graphics, so the world shows through and clicks there reach it.

**World.** `Prefabs/World/World.prefab` (`GameWorld`) holds a background camera (clears the screen, carries the AudioListener), the world camera (`WorldCamera`), a global 2D light, `Maps` (the mission's map instance) and the nested `Battlefield.prefab`. Sorting layers: Background, Terrain, Route, Default, Units, Effects; figures use a SortingGroup and sort by Y.

```text
AshenPassMap (AdventureMapAuthoring + AdventureMapView)     Battlefield (BattlefieldView)
  Background          map image, one cell per unit            Background
  Grid / Terrain Logic   painted logic layer, hidden in game   Hexes
  Route                                                       Units / Player Units, Enemy Units
  Towns, Friendly Units, Enemies, Heroes                      Effects (damage numbers)
  Hover Cell
```

`GameWorld` shows the map on the Adventure screen and the battlefield in battle, points the world camera at the screen's viewport each frame (its pixel rect follows the UI at any resolution) and turns pointer and arrow input into `Plan`, `ShowHero` or `BattleAction`. World input obeys the UI's blocking: nothing while a dialog, options or the F3 panel is open, and nothing through UI controls (an EventSystem raycast at the pointer). On the map the camera size comes from the cell's display size (UI design pixels per cell), follows the hero while he travels, pans with arrows or a drag and is clamped to the map; on the battlefield it frames the board. Runtime only instantiates saved prefabs (markers, route steps, hexes, damage numbers). `CanvasViewContext` picks live sprites or theme placeholders with Unity null checks for both UI and world figures.

Figures stand on their cell with a ground shadow in their side's colour; art of any size is scaled to one height with its feet on the ground line. Animations show resolved results and never decide gameplay: walk cycles play while a figure moves and it faces where it goes (battle stacks then face the enemy again, the hero keeps his last facing); the hero glides from cell to cell (jumps of more than six cells are shown at once), the acting stack has a pulsing ring in its side's colour and a highlighted hex, stacks glide, lunge, flash and fade, damage numbers rise and hits at or above a threshold shake the camera. Battle animations are keyed by combatant ID from `DamageEvent`.

## 7. Audio

`GameAudio` (Infrastructure) is the only adapter to the MLin Audio System. It plays the AudioDatabase field for each `SoundCue` and the unit's own voice for each `UnitVoiceRequested`, plays the ambient/gallop layers sample-aligned with `AudioSystem.PlayLayers` and fades them by travel state with `SoundHandle.SetVolume`, plays one music track with `AudioSystem.PlayMusic` from the session's `PlaybackState` (map, town from the mission's settings; battle, defeat from the AudioDatabase), the riding loop while riding, and turns each `DamageEvent` into a swing or release followed by a hit or miss on its own schedule (presentation timing only; the battle is already resolved). Unit voices replace each other. Channels Master → Music/Effects/Voice; music ducks 6 dB under voices; everything mutes without window focus. Player volumes persist through the audio system's built-in PlayerPrefs storage (`Save Volumes` in `GameAudioSettings`), independent of campaign saves. Validation requires a looping sound for every layer and the riding loop, a sound for every cue and music track; a unit without a voice is silent. The F3 panel lists every layer, music track, the riding loop, every cue and unit voice for audition.

Battle rewards are rolled by `CampaignRules.RollRewards` with the injected random source and the mission's `RewardPolicy` (gold variation, artefact/rare/legendary chances, legendary guarantee), then applied atomically by `ApplyVictory` and saved. `GameSession` keeps the run of victories without a legendary artefact for the session only (a testing aid, not saved). After every battle action the session waits the mission's `Battle Action Seconds` (the enemy at least `Enemy Turn Seconds`) before the next action; player commands wait meanwhile. A decided battle stays on the battlefield for the mission's `Battle Result Seconds` (player commands wait; the victory is already applied) before the session opens Victory or Defeat. After a won battle the session moves Victory → Rewards → None; the rewards screen only displays what was applied. Misses are rolled in `BattleEngine` from `CombatPolicy.MissChance` (no roll at 0) and carried on `DamageEvent` together with whether the strike was a ranged shot.

## 7a. Dialogue

Each conversation has a style: `PortraitBox` (portrait, name and line) or `SpeechBalloon` (a comic cloud with the line and a trail of bubbles from the speaker; the bubbles and the cloud pop in one after another before the line types). `DialogueDirector.Play(conversationId)` shows a conversation where its view is authored, `Play(conversationId, normalizedPosition)` anywhere in the design frame from (0,0) bottom-left to (1,1) top-right, and `Play(conversationId, rectTransform)` at a UI element on any canvas. For the box the point is where the box sits (its pivot follows the point, so it never leaves the screen); for the balloon it is the speaker, and the cloud is mirrored and clamped to stay on screen. Lines type at the database's characters per second in unscaled time with `TMP_Text.maxVisibleCharacters` (the text is parsed once per line, rich-text tags do not count). A click on the box shows the rest of a typing line, then moves to the next; after the last line the box closes and `Completed(conversationId)` is raised. Playing again replaces the running conversation; `Stop()` closes without `Completed`. A line's voice-over starts with it and stops when the line changes or the box closes; route it to the Voice channel so music ducks. The director validates the database at startup (unique speaker, conversation and line IDs, known speakers, non-empty text, voice-overs with clips) and a broken database stops the game from starting, like the other databases. The F3 panel has a button per conversation that opens it at a random point.

**Site conversations.** `GameSession.SiteReached` (Application) is raised after the hero reaches a friendly or hostile encounter (its dialog has opened) or the town (its screen has opened) from the map. `SiteDialogueCues` (Bootstrap) maps the encounter ID to the `Arrival Conversation Id` authored on the mission's encounter and plays it. For an encounter it calls `GameHud.SetDialogHeld(true)`: the session is already in the encounter dialog, so gameplay stays blocked and the session never waits for presentation; only the dialog's window stays hidden until the conversation completes. A speech-balloon conversation at an encounter rises from the encounter's cell on the map (`GameWorld.ScreenPoint`, passed in by the bootstrap), a portrait box opens where it is authored. If the session leaves that dialog meanwhile (Escape), the conversation stops and the hold is released. In the town the garrison hero speaks in a balloon from `TownCanvasView.GarrisonHeroAnchor`, one frame later so the town screen is laid out, and only while the clan holds the town. Unknown conversation IDs stop the game from starting. The smoke scenario runs without `SiteDialogueCues`, since it expects encounter dialogs at once.

## 8. Persistence

`JsonCampaignStore` writes schema version 3 with stable string IDs to `campaign.json` under `Application.persistentDataPath`: temporary file, then `File.Replace` keeping a `.bak`. Saves of earlier versions were made on the previous map with other movement points and are rejected rather than migrated; the game then starts a new mission. A load is fully validated (IDs, counts, ownership, positions) before it replaces the live session; a failure keeps both the session and the previous save. Autosave happens after travel, recruitment, equipment and victory. Tests and the smoke scenario use their own filenames.

## 9. Build and verification

- `Tools/Build.ps1`: EditMode tests (`Reports/editmode-tests.xml`), scene generation, Windows development player. Any failing test stops the build.
- `Tools/SmokeTest.ps1`: the built player runs `RuntimeSmokeRunner` with isolated save data, plays the whole mission (conquest, home/visiting, save/load, victory, defeat, retry), clicks the map and the board through the world input, checks camera follow, pan limits, viewport alignment, raycasts and resolutions and captures screenshots into `Reports/` for inspection.
- Human review: listening tests over a full playthrough, readability and pacing.

## 10. Decisions

Format: context, decision, tradeoff, validation, revisit trigger.

**Layers and composition.** Small team, fast iterations, rules must be testable without Unity. Five runtime assemblies with Domain/Application engine-free and manual composition in `GameBootstrap`; no DI container or service locator. Tradeoff: some mapping code at boundaries. Validated by EditMode tests on plain classes. Revisit if composition grows beyond one bootstrap.

**Content databases.** Inspector-editable ScriptableObject databases are the authored source, projected into plain read-only catalogs. Tradeoff: validation and save-ID migration work. Validated by content tests and edited values showing up in the built game. Revisit per-entity assets when content volume or merge conflicts warrant it.

**UI.** Prefab-authored uGUI with typed field bindings instead of string keys, so missing references are reported by name and renames are safe. The earlier UI generators were removed so Prefab Mode edits are permanent. Validated by `UiPrefabTests` and the built-player scenario. Switching UI frameworks needs an explicit user decision.

**Audio.** The in-project director duplicated voice limiting, buses and fades; playback now uses the MLin Audio System package pinned to a release tag in `Packages/manifest.json`. Tradeoff: an external dependency whose upgrades must be tested. Validated by audio tests and the built-player scenario. Revisit if the package stops covering a needed feature.

**Sound references.** String cue IDs could be mistyped and only failed at runtime. A unit's voice belongs to the unit, so it is a direct `SoundDefinition` reference on the unit entry. Fixed game events belong to no entity, so they are a `SoundCue` enum with one typed field each on the AudioDatabase. Tradeoff: a new event cue needs an enum value and a field (a code change), which suits a small fixed set. Validated by content tests (every cue and layer assigned, voices on the Voice channel). Revisit string IDs if data-driven or modded cues become a requirement.

**World presentation.** The map will be larger than the screen and needs effects, so it left the Canvas: a Canvas rebuilds on every moving element, has no camera, particles or 2D lights, and scrolling would need masks. The map and the battlefield are world objects behind viewports in the uGUI screens; the battlefield follows for effects and consistency. Tradeoff: two input paths (UI and world) that must agree on blocking, and a camera to keep aligned with the UI. Validated by world tests (cell and hex round trips) and the built-player scenario (clicks through the world, blocked clicks under modals, viewport alignment at several resolutions). Revisit only if the world needs 3D.

**Movement model.** The painted map has many terrain types and needs penalties between whole cells, so movement follows classic strategy games: points ×100 (1600 per day), a cost per terrain in an authored terrain database, diagonals 1.41 times dearer and trees, mountains, water, lava and obstacles impassable for a mounted hero. Tradeoff: saves from before the change are incompatible. Validated by terrain-rule and routing tests and the whole-mission scenario. Revisit with terrain-specific hero skills or boats.

**Full HD design.** The target is 1920x1080 at 16:9, so the UI reference resolution is 1920x1080 and map images are shown pixel for pixel there; other aspect ratios letterbox. Validated by the built-player scenario at 1920x1080, 1280x1024 and 1920x800.

**Map authoring.** A painted image per map with a hand-painted Tilemap logic layer, rather than terrain rows in the mission asset; the cell size is a setting with a Rebuild that resamples what is painted. Tradeoff: the logic layer must be painted to match the image, and start/encounter cells must be checked after a cell-size change. Validated by the logic-layer and resample tests and content validation (`IsBuilt`, walkable start and encounters). Revisit when maps are built from visual tiles; the logic layer can then be derived from them.

**URP.** Switched from the built-in pipeline before any world art exists, so nothing needed converting; the 2D Renderer brings 2D lights, sprite shaders and post-processing for the planned effects. Tradeoff: more render settings, URP-only shaders. Validated by the built-player screenshots. Revisit only for a platform requirement.

**Dialogue data.** Dialogue needs designer-editable text, speakers and optional voice-overs, independent of the rest of the game. One ScriptableObject database, like the other content: speakers by stable ID, conversations of lines with stable IDs, and the voice-over as a direct `SoundDefinition` reference rather than a voice-over ID string, following the sound-references decision (no mistyped IDs, the Inspector shows what plays, validation checks clips). Line IDs stay the key for recording scripts and later localization. The feature is its own assembly with no reference to any other game assembly, and voice-overs go through `IDialogueVoice` so tests run without the audio system. Tradeoff: a second small MLin adapter beside `GameAudio`; text in the asset is single-language. Presentations share a `DialogueView` base and the conversation picks one by `DialogueStyle`, so a new style is a new view and enum value, not a new director. Which site plays which conversation is content on the mission's encounters, and the game reaches dialogue only through Bootstrap (`SiteDialogueCues`), keeping Application, Presentation and the dialogue assembly independent. Validated by `DialogueTests` (validation, typing, advancing, voice-overs, placement, prefab setup). Revisit with the Unity Localization package (string tables keyed by line ID) when a second language is needed, or a spreadsheet/JSON import when writers work outside Unity or the line count grows into the thousands.

**Tests.** Unity Test Framework replaced custom Editor verification classes; the built-player scenario stays for whole-mission checks. Revisit with PlayMode tests when behaviour depends on frame timing.

## 11. Next steps

Correct the first-draft logic layer of the Ashen Pass map by hand, wood/iron UI frames (9-slice), item icons, Doom Rider art, a town screen built on the Ashen Hold concept (needs a version without baked UI), camera zoom, the 10-slot equipment layout from the design document, configurable artefact effects, more mission/town/economy tuning moved into focused ScriptableObjects, bar-synchronized music changes once distinct tracks exist, and PlayMode tests. Keep every step playable and buildable, with one authoritative implementation per rule.

Update this document when module boundaries, state ownership, the save format or content/audio contracts change.
