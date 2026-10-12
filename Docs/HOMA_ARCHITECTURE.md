# Heroes of Metal Age — architecture

Status: prototype 0.4, Unity 6000.3.26f1, Windows. This document records only what is specific to this project: module map, ownership, contracts and decisions. General Unity coding and architecture conventions (naming, formatting, lifecycle, uGUI layout/input/performance, SOLID, testing levels) come from the shared Unity guidance and are not repeated here. Read together with `PROTOTYPE.md` and `AUDIO_DIRECTION.md`.

## 1. Project requirements

- **Content:** all designer-editable content and tuning live in ScriptableObject assets, so stats, text, images and sounds change in the Inspector without code. Runtime state and saves stay separate.
- **UI:** the whole UI, including the F3 development panel, is prefab-authored uGUI with TextMeshPro. No runtime code uses IMGUI or builds controls.
- **Scope:** sound-first, turn-based, single player. Multiplayer, ECS, network determinism and modding are not current requirements.

## 2. Module map

Paths are relative to `Assets/HeroesOfMetalAge/`. Each row is an assembly definition.

| Assembly | Contents | Known limits |
|---|---|---|
| `Runtime/Domain` (no engine refs) | Definitions and coordinates, `CampaignState`, `CampaignRules`, `WorldMap` pathfinding, `HexGrid`, `BattleEngine`, `DamageCalculator`, `BattleAi`, `CampaignSnapshot` | One hero, one town, one encounter of each kind |
| `Runtime/Application` (no engine refs) | `GameSession` (commands, transitions, revisions, typed events), `SoundCue`/`MusicLayer` enums, ports `ISaveStore` and `IAudioControls` | Synchronous commands, no networking |
| `Runtime/Presentation` | `GameHud` router, one `CanvasScreenView` per screen, header/footer/notifications, `DebugPanelView`, `UiWidget`, `UiTheme`, `CanvasViewContext` | Placeholder art |
| `Runtime/Infrastructure` | Content databases, `ContentValidator`, `ContentLoader`; `GameAudio`; `JsonCampaignStore` | No live content refresh or streaming |
| `Runtime/Bootstrap` | `GameBootstrap` composition root, development-only `RuntimeSmokeRunner` | — |
| `Editor` | `ContentAssets` locator, `PrototypePipeline` (scene generation, build) | — |
| `Tests/EditMode` | Rules, battle/world, persistence, content, audio routing, UI prefabs | No PlayMode tests yet |

```text
Presentation ----> Application ----> Domain
Infrastructure --> Application ports / Domain
Bootstrap -------> everything (composition only)
Editor, Tests ---> runtime assemblies
```

Infrastructure references the `MLin.Audio` package assembly; `AssemblyInfo.cs` exposes internals to Editor and Tests. Presentation and Infrastructure never reference each other. Namespaces follow the layer (`HeroesOfMetalAge.Domain`, `.Application`, ...); split by feature only when a layer grows.

## 3. State and data ownership

| Data | Owner | Examples |
|---|---|---|
| Authored definitions | ScriptableObject databases → read-only `DefinitionCatalog` | Unit stats, artefact bonuses, mission map, cue mapping |
| Runtime state | `GameSession` → `CampaignState`, `BattleEngine` | Gold, armies, day, hero position, town owner, battle HP |
| Save format | `CampaignSnapshot` (DTO) via `ISaveStore` | Schema version, stable IDs |

- One `GameSession` owns the campaign and at most one battle. Domain operations own every write; views get read-only state and audio gets typed events.
- `ContentLoader` validates the databases (`ContentValidator`) and projects them into plain definitions once at startup. Domain never sees ScriptableObjects; Presentation and `GameAudio` keep the Sprite/SoundDefinition references.
- Sounds are referenced directly, never by string ID. A unit entry holds its own voice `SoundDefinition`; the AudioDatabase holds one field per `SoundCue` and `MusicLayer`. Gameplay publishes the enum value or the unit ID, and `GameAudio` resolves the sound.
- Asset edits apply on the next Play Mode start or build. New Mission resets the campaign against the loaded catalog without reloading it.

### Content assets (`Content/`)

| Asset | Class | Holds |
|---|---|---|
| `Databases/UnitDatabase.asset` | `UnitDatabaseScriptableObject` | ID, name, description, portrait, health, damage range, attack/defense, speed, ranged, price, weekly growth, voice (`SoundDefinition`) |
| `Databases/ArtefactDatabase.asset` | `ArtefactDatabaseScriptableObject` | ID, name, description, icon, slot, attack/defense/speed bonus |
| `Databases/HeroDatabase.asset` | `HeroDataBaseScriptableObject` | ID, name, portrait, faction/class, base stats, movement, progression, starting army |
| `Databases/AudioDatabase.asset` | `AudioDatabaseScriptableObject` | Audio system settings; a `SoundDefinition` field per music layer and per `SoundCue`; layer mix, layer fade time, heavy-hit threshold |
| `Databases/AshenPass.asset` | `MissionSettingsScriptableObject` | Map terrain, encounters and rewards, gold, town/faction, prices, combat tuning, pacing |
| `Audio/GameAudioSettings.asset`, `Audio/GameMixer.mixer`, `Audio/Sounds/*.asset` | MLin settings, mixer, `SoundDefinition` | Channels, ducking rule, focus muting; one sound per cue |
| `UI/UiTheme.asset` | `UiTheme` | TMP font, placeholder sprites, terrain and hex colours |

The class names above (including `HeroDataBaseScriptableObject`) are fixed by user request. Every content asset is authored, never regenerated; only `Assets/Scenes/AshenPass.unity` is generated (Tools > Heroes of Metal Age > Generate scene).

## 4. Commands and events

Views call `GameSession` methods: `Plan`, `Travel`, `EndDay`, `AcceptFriendly`, `FightEncounter`, `BattleAction`, `ContinueVictory`, `PrepareAfterDefeat`, `Buy`, `Transfer`, `StationHero`, `ClaimTown`, `Equip`, `Unequip`, `Save`, `Load`, `Restart` (plus `Debug*` for the development panel). Each command validates the whole outcome first, applies it atomically through `CampaignRules`/`BattleEngine` and returns or publishes a `CommandResult`. A disabled button is a convenience, never the validation.

`GameSession.Revision` increments on every state change; `GameHud` redraws only when it changes and widgets skip unchanged values. Audio receives `CueRequested` (`SoundCue`), `UnitVoiceRequested` (unit ID), `PlaybackChanged` and `DamageResolved` after a command succeeds; a listener failure never rolls back gameplay. `GameSession.IsBusy` is the single transition guard against overlapping travel, town and combat actions.

## 5. Game rules the architecture must preserve

- Adventure grid (`WorldPosition`) and combat hexes (`HexPosition`) are separate value types. Pathfinding allows diagonals without corner cutting and breaks ties deterministically; travel stops before an unresolved encounter or when movement runs out.
- `EndDay` restores movement and applies weekly growth on days 8, 15, 22…; loading a save never applies growth again.
- Combat damage, retaliation (once per round), ranged half-damage in melee, defending (−30 %) and casualties are resolved by `BattleEngine` independently of animation. Randomness comes from an injected `IRandomSource`; tests use fixed seeds. `BattleAi` acts through the same validated actions as the player.
- Victory rewards are granted once, even if the terminal action repeats. Defeat offers a retry from the pre-battle snapshot without persistent casualties.
- Town ownership is explicit. The neutral clan garrison cannot be transferred; owning the town unlocks transfers and 55 % recruit prices. Home and visiting heroes are distinct; stationing merges armies only after the full destination is validated.
- Hero stats are base + level + equipped bonuses, always computed, never written back into base stats.

## 6. UI composition

`GameUiRoot.prefab` (under `Prefabs/UI/`) owns the CanvasScaler (1600×900 design frame, Expand, letterboxed), the single EventSystem (legacy input module) and nested screen prefabs: `Adventure`, `Combat`, `Town`, `Hero`, `Dialog`, `Options`, `Header`, `Footer`, `Notifications`. `DebugPanel.prefab` is instantiated under the design frame only in the Editor and development builds. Nine shared widgets live in `Widgets/`, including separate WorldEntity and CombatUnit markers.

Each view binds its controls through named, typed serialized fields (`titleText`, `rideButton`, `portraitImage`) and reports a missing reference by field name on startup. Runtime only instantiates saved prefabs (tiles, hexes, slots, recruit cards, artefacts, route segments, debug buttons) and binds data.

```text
Adventure
  Environment
    Grass Tiles / Forest Tiles / Road Tiles / Mountain Tiles / Water Tiles
  Route
  Friendly Units
  Enemies
  Towns
  Heroes
Combat
  Hexes
  Units
    Player Units
    Enemy Units
```

Initial tiles and markers are saved prefab instances visible in Prefab Mode; runtime rebinds them. Terrain is painted once per map bind. `CanvasViewContext` picks live sprites or theme placeholders using Unity null checks, so destroyed or unassigned art falls back safely. Options, dialogs and the development panel are modal and restore keyboard focus.

## 7. Audio

`GameAudio` (Infrastructure) is the only adapter to the MLin Audio System. It plays the AudioDatabase field for each `SoundCue` and the unit's own voice for each `UnitVoiceRequested`, plays the ambient/gallop/battle layers sample-aligned with `AudioSystem.PlayLayers` and fades them by travel/combat state with `SoundHandle.SetVolume`. Unit voices replace each other. Channels Master → Music/Effects/Voice; music ducks 6 dB under voices; everything mutes without window focus. Player volumes persist through the audio system's built-in PlayerPrefs storage (`Save Volumes` in `GameAudioSettings`), independent of campaign saves. Validation requires a looping sound for every layer and a sound for every cue; a unit without a voice is silent. The F3 panel lists every layer, cue and unit voice for audition.

## 8. Persistence

`JsonCampaignStore` writes schema version 2 with stable string IDs to `campaign.json` under `Application.persistentDataPath`: temporary file, then `File.Replace` keeping a `.bak`. Version 1 saves migrate on read through explicit legacy ID maps. A load is fully validated (IDs, counts, ownership, positions) before it replaces the live session; a failure keeps both the session and the previous save. Autosave happens after travel, recruitment, equipment and victory. Tests and the smoke scenario use their own filenames.

## 9. Build and verification

- `Tools/Build.ps1`: EditMode tests (`Reports/editmode-tests.xml`), scene generation, Windows development player. Any failing test stops the build.
- `Tools/SmokeTest.ps1`: the built player runs `RuntimeSmokeRunner` with isolated save data, plays the whole mission (conquest, home/visiting, save/load, victory, defeat, retry), checks raycasts and resolutions and captures screenshots into `Reports/` for inspection.
- Human review: listening tests over a full playthrough, readability and pacing.

## 10. Decisions

Format: context, decision, tradeoff, validation, revisit trigger.

**Layers and composition.** Small team, fast iterations, rules must be testable without Unity. Five runtime assemblies with Domain/Application engine-free and manual composition in `GameBootstrap`; no DI container or service locator. Tradeoff: some mapping code at boundaries. Validated by EditMode tests on plain classes. Revisit if composition grows beyond one bootstrap.

**Content databases.** Inspector-editable ScriptableObject databases are the authored source, projected into plain read-only catalogs. Tradeoff: validation and save-ID migration work. Validated by content tests and edited values showing up in the built game. Revisit per-entity assets when content volume or merge conflicts warrant it.

**UI.** Prefab-authored uGUI with typed field bindings instead of string keys, so missing references are reported by name and renames are safe. The earlier UI generators were removed so Prefab Mode edits are permanent. Validated by `UiPrefabTests` and the built-player scenario. Switching UI frameworks needs an explicit user decision.

**Audio.** The in-project director duplicated voice limiting, buses and fades; playback now uses the MLin Audio System package pinned to a release tag in `Packages/manifest.json`. Tradeoff: an external dependency whose upgrades must be tested. Validated by audio tests and the built-player scenario. Revisit if the package stops covering a needed feature.

**Sound references.** String cue IDs could be mistyped and only failed at runtime. A unit's voice belongs to the unit, so it is a direct `SoundDefinition` reference on the unit entry. Fixed game events belong to no entity, so they are a `SoundCue` enum with one typed field each on the AudioDatabase. Tradeoff: a new event cue needs an enum value and a field (a code change), which suits a small fixed set. Validated by content tests (every cue and layer assigned, voices on the Voice channel). Revisit string IDs if data-driven or modded cues become a requirement.

**Tests.** Unity Test Framework replaced custom Editor verification classes; the built-player scenario stays for whole-mission checks. Revisit with PlayMode tests when behaviour depends on frame timing.

## 11. Next steps

Richer art (fixed Ashen Pass background with the logical grid adapted to it, wood/iron UI frames), the 10-slot equipment layout from the design document, configurable artefact effects, more mission/town/economy tuning moved into focused ScriptableObjects, bar-synchronized music changes once distinct tracks exist, and PlayMode tests. Keep every step playable and buildable, with one authoritative implementation per rule.

Update this document when module boundaries, state ownership, the save format or content/audio contracts change.
