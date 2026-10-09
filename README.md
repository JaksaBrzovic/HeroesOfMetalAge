# Heroes of Metal Age — first playable prototype

Unity **6000.3.26f1**, Windows x64, Mono, built-in rendering. An original sound-driven turn-based strategy prototype inspired by classic adventure-map and tactical-combat games.

## Play

Run `Builds/Windows/HeroesOfMetalAge.exe`, or `Play.cmd` at the project root. No Unity Editor is required to play.

Click a map tile to preview the route, then click it again or press Space to travel. E ends the day. H opens the hero. Escape opens options or returns from town/hero. M mutes. F3 opens the development and audio-audition panel.

Mission: welcome six Ash Archers, defeat the pass wardens, enter Ashen Hold and recruit troops, then defeat the Black Banner. Inspect the hero and equip the Fang of Thunder after the first victory.

Battle: blue hexes show reachable cells. Click an empty blue hex to move, or an enemy to attack. Melee units can move to an adjacent reachable hex and attack in one action. Archers can attack at range but deal half damage in melee. Each stack gets one retaliation per round; defending reduces incoming damage by 30%. Turns follow creature speed.

Town: choose the recruit quantity and purchase any of three creature types. MAX purchases the largest affordable quantity for each type. Neutral garrison belongs to Lord Varg of the Ashen Clan and cannot be transferred. You can optionally fight the clan to own the town, unlock transfers and reduce recruit prices to 55%. In an owned town, click a filled army slot to transfer its full stack to the other row. Click Kael's hero slot to station him at home or return him to visiting. Stationing merges his army with the home army; return him to visiting before recruitment or travel. Stocks accumulate on days 8, 15, 22, etc.

Campaign saves are stored in `%USERPROFILE%/AppData/LocalLow/Iron Oath Studio/HeroesOfMetalAge/campaign.json`. Safe-state movement, recruitment, equipment and victories autosave. Options also provide manual save/load. A defeat permits retrying with the pre-battle army or returning to prepare. Starting a new mission resets the in-memory campaign; the existing save is replaced by the next autosave.

## CLI workflow

```powershell
./Tools/Build.ps1
./Tools/SmokeTest.ps1
```

The build command runs Unity batchmode, invokes Editor setup, validates campaign/combat rules and writes the Windows player. SmokeTest runs the built player with isolated campaign data, simulates the mission, tests victory and defeat, and captures the world, battle, hero and town screens. Reports are in `Reports/`.

## Current scope and limitations

Implemented: square adventure grid, diagonal pathfinding without corner cutting, movement budget, days/weeks, friendly joining, blocking hostile encounter, hex tactical combat, retaliation and ranged troops, simple enemy AI, victory/defeat panels, XP/levels, gold/rewards, equipped artifacts and inventory, neutral/owned town recruitment, two army rows, weekly growth, town conquest, save/load and sound-reactive playback.

Art is original procedural silhouette/tile placeholder art. Music is original synthesized ambient and distorted metal-style riffs, not recorded guitars. Creature calls are offline Microsoft David speech placeholders. The final sound experience needs authored guitar stems, performance, mixing and user listening tests. Town construction/investment, multiple playable heroes, negotiation, spells, campaign selection, multiplayer and drag/drop partial stack splitting are outside this first slice.

All interface text is currently English. The game is not an asset or interface copy of Heroes of Might and Magic III.
