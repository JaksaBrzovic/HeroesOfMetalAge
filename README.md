# Heroes of Metal Age — prototype 0.4

Unity **6000.3.26f1**, Windows x64, Mono, built-in rendering. An original sound-driven turn-based strategy prototype inspired by classic adventure-map and tactical-combat games.

## Game design

The living [Game Design Document](Docs/GAME_DESIGN.md) describes the game vision, implemented rules and content, audio/visual direction, future systems and open decisions. Update it alongside development. ScriptableObject assets remain the source for actual tuning values.

## Play the prototype

Run `Builds/Windows/HeroesOfMetalAge.exe`, or `Play.cmd` at the project root. No Unity Editor is required to play.

Click a map tile to preview the route, then click it again or press Space to travel. E ends the day. H opens the hero. Escape opens options or returns from town/hero. M mutes. F3 opens the development panel (audition every sound, campaign shortcuts).

Mission: welcome six Ash Archers, defeat the pass wardens, enter Ashen Hold and recruit troops, then defeat the Black Banner. Inspect the hero and equip the Fang of Thunder after the first victory.

Battle: blue hexes show reachable cells. Click an empty blue hex to move, or an enemy to attack. Melee units can move to an adjacent reachable hex and attack in one action. Archers can attack at range but deal half damage in melee. Each stack gets one retaliation per round; defending reduces incoming damage by 30%. Turns follow creature speed.

Town: choose the recruit quantity and purchase any of three creature types. MAX purchases the largest affordable quantity for each type. Neutral garrison belongs to Lord Varg of the Ashen Clan and cannot be transferred. You can optionally fight the clan to own the town, unlock transfers and reduce recruit prices to 55%. In an owned town, click a filled army slot to transfer its full stack to the other row. Click Kael's hero slot to station him at home or return him to visiting. Stationing merges his army with the home army; return him to visiting before recruitment or travel. Stocks accumulate on days 8, 15, 22, etc.

Campaign saves are stored in `%USERPROFILE%/AppData/LocalLow/Iron Oath Studio/HeroesOfMetalAge/campaign.json`. Safe-state movement, recruitment, equipment and victories autosave. Options also provide manual save/load. A defeat permits retrying with the pre-battle army or returning to prepare. Starting a new mission resets the in-memory campaign; the existing save is replaced by the next autosave.

## CLI workflow

```powershell
./Tools/Build.ps1
./Tools/SmokeTest.ps1
```

`Build.ps1` runs the EditMode tests (`Assets/HeroesOfMetalAge/Tests/EditMode`, results in `Reports/editmode-tests.xml`), regenerates the bootstrap scene and writes the Windows development player. `SmokeTest.ps1` runs the built player with isolated campaign data, plays the whole mission including victory, defeat and retry, and captures the world, battle, hero, town, options and development-panel screens into `Reports/`. The tests also run from Unity's Test Runner window.

## Audio

Audio runs on the [MLin Audio System](https://github.com/JaksaBrzovic/MLinAudioSystem) package (`com.manalink.audiosystem`, pinned to a release tag in `Packages/manifest.json`).

- `Content/Databases/AudioDatabase.asset` has one named field per game event (Victory, Heavy Hit, Click…) and per music layer, each pointing to a `SoundDefinition` in `Content/Audio/Sounds/`. Unit voices are assigned on each unit in `UnitDatabase.asset`. Replace clips, add variations or change volume, pitch, priority, instance limit or cooldown on the SoundDefinition; no code changes.
- `Content/Audio/GameAudioSettings.asset` and `GameMixer.mixer` hold the Master/Music/Effects/Voice channels, the voice-over-music ducking rule and focus muting.
- The ambient, gallop and battle layers start sample-aligned and are mixed by travel and combat state (`Layer mix` on the AudioDatabase).

## Edit content without code

Open `Assets/HeroesOfMetalAge/Content/Databases/` in Unity and select:

- `UnitDatabase.asset`: health, damage, attack/defense, speed, price, weekly growth, portrait and voice sound for each unit.
- `ArtefactDatabase.asset`: name, description, icon, equipment slot and attack/defense/speed bonuses.
- `HeroDatabase.asset`: hero name, portrait, description, faction/class, base stats, movement, progression and starting army.
- `AudioDatabase.asset`: the sound for each game event and music layer, the layer mix and the heavy-hit threshold (see Audio above).
- `AshenPass.asset`: map terrain, encounter armies/rewards, starting gold, town/faction, prices, combat tuning and pacing.

Stable IDs are used by saves and cross-references. Reorder entries freely, but do not rename/remove IDs without migrating their references/saves. Stop and restart Play Mode or rebuild the player to pick up edited definitions. In-game New Mission uses the existing catalog snapshot. Portraits/icons are optional and use the placeholder art from `Content/UI/UiTheme.asset` when unassigned; the theme also holds the map and board colours.

Runtime is split into Domain, Application, Presentation, Infrastructure and Bootstrap assemblies under `Assets/HeroesOfMetalAge/Runtime/`. Domain and Application have no Unity references. Views issue validated commands; audio and persistence are separate adapters. See `Docs/HOMA_ARCHITECTURE.md` for ownership, contracts and decisions.

Version 1 prototype saves migrate automatically on read. Version 2 saves use stable IDs; successful replacements retain a `.bak` backup. Tests and the smoke scenario never use the normal player campaign file.

## Edit UI prefabs

The entire UI uses Unity Canvas/uGUI and TextMeshPro, including the F3 development panel. Open `Assets/HeroesOfMetalAge/Prefabs/UI/GameUiRoot.prefab` in Prefab Mode; edit its nested screen prefabs under `Screens/` and shared controls under `Widgets/`. Every view references its controls through named, typed fields in the Inspector (for example `titleText`, `rideButton`); a missing reference is reported by name when the game starts and by the UI tests.

Adventure contains an `Environment` group split into Grass/Forest/Road/Mountain/Water Tiles, plus separate `Heroes`, `Enemies`, `Friendly Units`, `Towns` and `Route` groups. Initial tiles/entities are authored in the prefab and visible in Prefab Mode. Combat similarly separates `Hexes` from `Units/Player Units` and `Units/Enemy Units`. Actor markers use dedicated prefab instances and remain independent of terrain/hex backgrounds.

Runtime creates instances of saved prefabs for map tiles, hexes, recruitment cards, army slots, artefacts and development-panel buttons; it does not construct controls or draw screens through OnGUI. The prefabs are authored assets: tooling never regenerates them, so Prefab Mode edits are permanent. Only `Assets/Scenes/AshenPass.unity` is generated (Tools > Heroes of Metal Age > Generate scene).

The root retains the legacy EventSystem input module. CanvasScaler Expand fits the 1600x900 design frame on other aspect ratios with letterboxing. Popup dialogs, options and the development panel block gameplay clicks and restore/select keyboard focus.

## Current scope and limitations

Implemented: square adventure grid, diagonal pathfinding without corner cutting, movement budget, days/weeks, friendly joining, blocking hostile encounter, hex tactical combat, retaliation and ranged troops, simple enemy AI, victory/defeat panels, XP/levels, gold/rewards, equipped artifacts and inventory, neutral/owned town recruitment, two army rows, weekly growth, town conquest, save/load and sound-reactive playback with ducking and focus muting.

Art is original procedural silhouette/tile placeholder art. Music is original synthesized ambient and distorted metal-style riffs, not recorded guitars. Creature calls are offline Microsoft David speech placeholders. The final sound experience needs authored guitar stems, performance, mixing and user listening tests. Town construction/investment, multiple playable heroes, negotiation, spells, campaign selection, multiplayer and drag/drop partial stack splitting are outside this first slice.

All interface text is currently English. The game is not an asset or interface copy of Heroes of Might and Magic III.

Sprite fallback uses Unity null semantics, so unassigned/destroyed references in Editor Play Mode correctly select placeholder art. After code refresh, stop the current Play session and start it again to recreate its presentation context.
