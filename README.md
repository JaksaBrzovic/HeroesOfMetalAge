AI-Native project developed by Jaksa Brzovic

<!-- usage:start -->
Claude Code time: 5 h 6 min of work over 6 sessions

| Claude Code usage | Input | Output | Cache write 5m | Cache write 1h | Cache read | Web searches | Web fetches |
|---|---:|---:|---:|---:|---:|---:|---:|
| claude-opus-5-5 | 2,140 | 1,364,152 | 0 | 5,582,526 | 423,818,682 | 0 | 0 |
<!-- usage-data: {"models": {"claude-opus-5-5": {"cache_read": 423818682, "cache_write_1h": 5582526, "cache_write_5m": 0, "input": 2140, "output": 1364152, "web_fetches": 0, "web_searches": 0}}, "seconds": 18407.252, "sessions": 6, "until": "2026-10-15T12:09:09+00:00"} -->
<!-- usage:end -->

Codex time (2026-10-09 – 2026-10-12, finished): 6 h 35 min of work over 5 sessions

| Codex usage | Input (uncached) | Cached input | Output | of which reasoning |
|---|---:|---:|---:|---:|
| gpt-6.1-sol | 2,658,784 | 68,483,456 | 354,087 | 95,575 |

# Heroes of Metal Age — prototype 0.7

Unity **6000.3.26f1**, Windows x64, Mono, Universal Render Pipeline (2D Renderer). An original sound-driven turn-based strategy prototype inspired by classic adventure-map and tactical-combat games. Target resolution: Full HD (1920x1080, 16:9); the UI is designed at 1920x1080 and scales to other sizes.

## Game design

The living [Game Design Document](Docs/GAME_DESIGN.md) describes the game vision, implemented rules and content, audio/visual direction, future systems and open decisions. Update it alongside development. ScriptableObject assets remain the source for actual tuning values.

## Play the prototype

Run `Builds/Windows/HeroesOfMetalAge.exe`, or `Play.cmd` at the project root. No Unity Editor is required to play.

Click a map cell to preview the route, then click it again or press Space to travel. The part of a new route that today's movement cannot pay for is red, and cells the hero has entered leave the route. Clicking another cell while he rides picks a new destination: he stops in the cell he is riding into with the new route planned, and clicking the new destination again (or Space) before he stops lets him ride on without stopping. The hero rides on roads, dirt, grass and other open ground; trees, mountains, water, lava and obstacles stop him. Each cell costs movement points (road 75, grass and dirt 100, ash and rough ground 125, sand 150, swamp 175; diagonal steps 1.41 times as much) from 1600 per day. Arrow keys or dragging the map (left, right or middle button) look around; the camera follows the hero while he rides. Click the hero to open him. E ends the day: an hourglass turns in the middle of the screen for three seconds with its sound, then the new day begins. H opens the hero. Escape opens options or returns from town/hero. M mutes. F3 opens the development panel (audition every sound, campaign shortcuts).

Mission: ride east along the road through the Ashen Pass valley, welcome six Air Guitarists at the clearing, defeat the pass wardens in the mountain pass, enter Ashen Hold and recruit troops, then defeat the Black Banner. Inspect the hero and equip the Fang of Thunder after the first victory.

Battle: blue hexes show reachable cells. Click an empty blue hex to move, or an enemy to attack. Melee units can move to an adjacent reachable hex and attack in one action. Air Guitarists strike at range but deal half damage in melee. Each stack gets one retaliation per round; defending reduces incoming damage by 30%. Turns follow creature speed.

Town: choose the recruit quantity and purchase any of three creature types. MAX purchases the largest affordable quantity for each type. Neutral garrison belongs to Lord Varg of the Ashen Clan and cannot be transferred. You can optionally fight the clan to own the town, unlock transfers and reduce recruit prices to 55%. In an owned town, click a filled army slot to transfer its full stack to the other row. Click Kael's hero slot to station him at home or return him to visiting. Stationing merges his army with the home army; return him to visiting before recruitment or travel. Stocks accumulate on days 8, 15, 22, etc.

Campaign saves are stored in `%USERPROFILE%/AppData/LocalLow/Iron Oath Studio/HeroesOfMetalAge/campaign.json`. Safe-state movement, recruitment, equipment and victories autosave. Options also provide manual save/load. A defeat permits retrying with the pre-battle army or returning to prepare. Starting a new mission resets the in-memory campaign; the existing save is replaced by the next autosave.

## CLI workflow

```powershell
./Tools/Build.ps1
./Tools/SmokeTest.ps1
```

`Build.ps1` runs the EditMode tests (`Assets/HeroesOfMetalAge/Tests/EditMode`, results in `Reports/editmode-tests.xml`), regenerates the bootstrap scene and writes the Windows development player. `SmokeTest.ps1` runs the built player with isolated campaign data, plays the whole mission including the victory, rewards and defeat screens and a retry, and captures the world, battle, victory, rewards, defeat, hero, town, options, development-panel and terrain-overlay screens into `Reports/`. The tests also run from Unity's Test Runner window.

## Audio

Audio runs on the [MLin Audio System](https://github.com/JaksaBrzovic/MLinAudioSystem) package (`com.manalink.audiosystem`, pinned to a release tag in `Packages/manifest.json`).

- `Content/Databases/AudioDatabase.asset` has one named field per game event (Victory, Sword Hit, Arrow Miss, Legendary Item…), per music layer, for the battle and defeat music and for the riding loop, each pointing to a `SoundDefinition` in `Content/Audio/Sounds/`. Unit voices are assigned on each unit in `UnitDatabase.asset`. Replace clips, add variations or change volume, pitch, priority, instance limit or cooldown on the SoundDefinition; no code changes.
- `Content/Audio/GameAudioSettings.asset` and `GameMixer.mixer` hold the Master/Music/Effects/Voice channels, the voice-over-music ducking rule and focus muting.
- The mission's map music plays on the adventure map and its town music in the town (`Music` on `AshenPass.asset`); the ambient and gallop layers lead only for a mission without map music (`Layer mix` on the AudioDatabase). The battle music (intro, then loop) plays while fighting and the defeat theme on the defeat screen; the hoofbeat loop plays while the hero rides.
- Strikes play a sword swing or an arrow release, then a hit or a miss (`Strikes` timing on the AudioDatabase); heavy hits add the heavy-hit layer.
- Recorded sounds and their licences are listed in `Docs/AUDIO_CREDITS.md`; CC-BY sounds must be credited.

## Edit content without code

Open `Assets/HeroesOfMetalAge/Content/Databases/` in Unity and select:

- `UnitDatabase.asset`: health, damage, attack/defense, speed, price, weekly growth, portrait, walk cycle and voice sound for each unit.
- `ArtefactDatabase.asset`: name, description, icon, equipment slot, rarity (common, rare, legendary) and attack/defense/speed bonuses.
- `HeroDatabase.asset`: hero name, portrait, map ride cycle, description, faction/class, base stats, movement points per day, progression and starting army.
- `TerrainDatabase.asset`: which terrain is passable, the movement cost of each and the diagonal step cost.
- `AudioDatabase.asset`: the sound for each game event, music layer and music track, the riding loop, the layer mix, strike timing and the heavy-hit threshold (see Audio above).
- `DialogueDatabase.asset`: the dialogue typing speed, the speakers (ID, name, portrait) and the conversations (ID and lines: ID, speaker ID, text, optional voice-over SoundDefinition on the Voice channel). See Dialogue below.
- `AshenPass.asset`: the map prefab, start and encounter cells, encounter armies, experience and typical gold, starting gold, town/faction, prices, the default and per-encounter battlefield images, combat tuning (including the miss chance), battle reward rolls and pacing (travel step, enemy turn, the pause after every battle action and the pause before the victory/defeat screen).

Stable IDs are used by saves and cross-references. Reorder entries freely, but do not rename/remove IDs without migrating their references/saves. Stop and restart Play Mode or rebuild the player to pick up edited definitions. In-game New Mission uses the existing catalog snapshot. Portraits/icons are optional and use the placeholder art from `Content/UI/UiTheme.asset` when unassigned; the theme also holds the hex colours.

Runtime is split into Domain, Application, Presentation, Infrastructure and Bootstrap assemblies under `Assets/HeroesOfMetalAge/Runtime/`. Domain and Application have no Unity references. Views issue validated commands; audio and persistence are separate adapters. See `Docs/HOMA_ARCHITECTURE.md` for ownership, contracts and decisions.

Saves (version 3) use stable IDs; successful replacements retain a `.bak` backup. Saves from earlier versions were made on the old map and are rejected; the game then starts a new mission. Tests and the smoke scenario never use the normal player campaign file.

## Dialogue

Dialogue can open at any time anywhere on the screen, in one of two styles chosen per conversation (`Style`):

- **Portrait box**: a dialogue box with the speaker's portrait on the left and the name and text on the right.
- **Speech balloon**: comic-strip style, small bubbles rise from the speaker to a cloud with the text; no portrait. (In comics a trail of small bubbles is the thought-balloon convention; a speech balloon usually has a pointed tail.)

Text types fast, letter by letter; a click on the dialogue shows the whole line, the next click moves on, and it closes after the last line. A line may have a recorded voice-over.

In the mission: reaching the Willing plays their portrait line before the join dialog appears, reaching the pass wardens plays theirs before the battle dialog, the Black Banner speaks in a balloon from its figure on the map before the battle dialog, and entering Ashen Hold while the clan holds it makes Lord Varg speak in a balloon from his garrison slot. Each encounter in `AshenPass.asset` names its conversation in `Arrival Conversation Id` (empty for none). Escape during an encounter line leaves the encounter as usual. The automated smoke scenario runs without these site conversations.

- Content: `Content/Databases/DialogueDatabase.asset`. Add a speaker, then a conversation with its lines; a single remark is a conversation of one line. IDs must be unique; validation names any problem at startup.
- Look: `Prefabs/UI/Widgets/DialogueBox.prefab` and `DialogueBalloon.prefab` (cloud and bubble sprites in `Content/UI/Sprites/`), both nested in `Prefabs/UI/DialogueOverlay.prefab`, its own Canvas above the game UI. Move the box in the overlay to change where it opens by default; on the balloon, `Balloon Offset` sets where the cloud sits from the speaker and `Pop Seconds` the speed of the bubbles popping in.
- Code: `DialogueDirector.Play("conversation-id")` at the authored spot, `Play("conversation-id", new Vector2(x, y))` with x, y from 0 (left/bottom) to 1 (right/top), or `Play("conversation-id", someRectTransform)` at a UI element. For a balloon the position is the speaker's. `Completed` reports the conversation that was read to the end. The director is created by `GameBootstrap`; the dialogue assembly knows nothing else of the game.
- F3 lists every conversation (`DIALOGUE ...`) and opens it at a random place.
- `Tools > Heroes of Metal Age > Create missing dialogue assets` (`DialogueAssetSeeder`, also run by Generate scene) was a one-off tool that created the initial database and prefabs; it only creates missing assets and never overwrites them, so it does nothing while they exist. It can be deleted together with its call in `PrototypePipeline.Setup`.
- `Tools > Heroes of Metal Age > Create missing end-of-day assets` (`EndDayAssetSeeder`, also run by Generate scene) was a one-off tool that created the `EndDayOverlay` prefab (the hourglass, from the sprites in `Content/UI/Sprites/EndDay/`) and nested it into `GameUiRoot`; it only creates the prefab when it is missing and only nests it when `GameHud` has no end-of-day view, so it does nothing while they exist. It can be deleted together with its call in `PrototypePipeline.Setup`.

## Adventure map and battlefield

The map and the battle board are drawn in the world by an orthographic camera; the UI stays on top as uGUI and leaves a viewport for them (`MapViewport` in Adventure, `BattleViewport` in Combat).

**Replace the map image and adjust the cells**

1. Import the map image (PNG) anywhere under `Assets/` and open `Content/Maps/AshenPass/AshenPassMap.prefab`.
2. On the root's **Adventure Map Authoring**, set `Map Image` and `Pixels Per Cell` (cells are square). The Ashen Pass image is 3200 x 1920 px at 64 px per cell = 50 x 30 cells; the Inspector shows this for any image.
3. Press **Rebuild**. It imports the image with one cell per world unit, sets the grid and keeps what is already painted: when the cell size changes, every new cell takes the terrain painted under its centre.
4. Paint the logic layer: select `Grid/Terrain Logic`, open **Window > 2D > Tile Palette**, choose the palette **Terrain Logic** and paint the terrain over the image (Road, Grass, Dirt, Rough, Sand, Swamp, Forest, Mountain, Water, Lava, Obstacle). Costs and passability come from `TerrainDatabase.asset`; unpainted cells are impassable. The current layer is a first draft made from the image: check it with the overlay and correct it with the palette.
5. Hover the map in the Scene view to read a cell's coordinates (from the top-left corner) and set the hero start and the encounter positions in `AshenPass.asset`. After changing the cell size, check these positions.

The logic layer is the only terrain the rules use and it is hidden in the game; the F3 panel's **TERRAIN OVERLAY** shows it over the image. `Display Pixels Per Cell` on the map's **Adventure Map View** sets how large a cell appears on screen (0 = the image's own pixel size); there is no zoom yet. At Full HD one image pixel is one screen pixel. The town of this map is painted on the image, so its marker is hidden (`Show Town Marker`); its cell in front of the gate is still clickable.

**Battlefield**: `Prefabs/World/Battlefield.prefab` holds the fixed background image and the hex board laid over it (`Board Center`, `Hex Width`; select it to preview the hexes in the Scene view). Images are chosen per encounter (`Battle Background` on each encounter in `AshenPass.asset`), else the mission's: the green vale for the pass, the infernal waste for Ashen Hold and the Black Banner.

Figures (`Prefabs/World/WorldMarker.prefab`) stand on their cell with a shadow in their side's colour and play their walk cycle while they move, facing where they go; in battle the stack whose turn it is has a pulsing ring and a highlighted hex. Figures lunge when they strike, flash when hit and fade when a stack dies; damage numbers rise from the target and heavy hits shake the camera. All of this only shows what the rules already resolved.

## Edit UI prefabs

The entire UI uses Unity Canvas/uGUI and TextMeshPro, including the F3 development panel. Open `Assets/HeroesOfMetalAge/Prefabs/UI/GameUiRoot.prefab` in Prefab Mode; edit its nested screen prefabs under `Screens/` and shared controls under `Widgets/`. Every view references its controls through named, typed fields in the Inspector (for example `titleText`, `rideButton`); a missing reference is reported by name when the game starts and by the UI tests.

The adventure map prefab groups `Background`, `Grid/Terrain Logic`, `Route`, `Towns`, `Friendly Units`, `Enemies` and `Heroes`; the battlefield groups `Background`, `Hexes`, `Units/Player Units`, `Units/Enemy Units` and `Effects`.

Runtime creates instances of saved prefabs for markers, route steps, hexes, damage numbers, recruitment cards, army slots, artefacts and development-panel buttons; it does not construct controls or draw screens through OnGUI. The prefabs are authored assets: tooling never regenerates them, so Prefab Mode edits are permanent. Only `Assets/Scenes/AshenPass.unity` is generated (Tools > Heroes of Metal Age > Generate scene).

The root retains the legacy EventSystem input module. CanvasScaler Expand fits the 1920x1080 design frame on other aspect ratios with letterboxing. Popup dialogs, options and the development panel block gameplay clicks, including clicks on the map and board, and restore/select keyboard focus.

## Current scope and limitations

Implemented: square adventure grid, diagonal pathfinding without corner cutting, movement budget, days/weeks, friendly joining, blocking hostile encounter, hex tactical combat, retaliation and ranged troops, misses, simple enemy AI, victory/rewards/defeat screens, XP/levels, rolled gold and artefact rewards with a legendary guarantee, equipped artifacts and inventory, neutral/owned town recruitment, two army rows, weekly growth, town conquest, save/load and sound-reactive playback with ducking and focus muting.

Art is original procedural placeholder art: the map and battlefield images are generated stand-ins until painted images are imported. The ambient and gallop layers and some short cues are original synthesized placeholders; the battle and defeat music, hoofbeats, strikes and encounter/victory/legendary stingers are free recorded sounds (see `Docs/AUDIO_CREDITS.md`). Creature calls are offline Microsoft David speech placeholders. The final sound experience needs authored guitar stems, performance, mixing and user listening tests. Town construction/investment, multiple playable heroes, negotiation, spells, campaign selection, multiplayer and drag/drop partial stack splitting are outside this first slice.

All interface text is currently English. The game is not an asset or interface copy of Heroes of Might and Magic III.

Sprite fallback uses Unity null semantics, so unassigned/destroyed references in Editor Play Mode correctly select placeholder art. After code refresh, stop the current Play session and start it again to recreate its presentation context.
