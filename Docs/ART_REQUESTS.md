# Art requests

Placeholders in the game that wait for painted art. Every image is replaced by dropping a new PNG over the old file (same name,
same folder) or by assigning another sprite in the named asset; no code changes. Style for all: hand-painted high-oblique
isometric map art like Heroes of Might and Magic III, Frank Frazetta heavy metal mood, warm low sun, transparent background,
no text, no UI, no frame, no ground shadow beyond the object's own footprint.

## Map objects (`Content/World/MapObjects/`, shown standing on one 64 px map cell; square canvas 512 x 512)

| File | Object | Prompt |
|---|---|---|
| `Chest.png` | An old chest | An iron-banded wooden treasure chest, lid ajar, gold coins spilling over the rim, on a patch of worn earth |
| `Colosseum.png` | The Bloody Colosseum | A small ruined stone arena with crumbling arches, a sand pit stained red, two crossed swords planted at its gate |
| `IronBastion.png` | The Iron Bastion | A squat stone watchtower with iron-plated gates and a large round shield hung over the door, banners in rust red |
| `ShrineOfHorns.png` | Shrine of the Horns | A standing stone carved with a hand raising the metal horns, braziers burning at both sides, skulls of horned beasts at its foot |
| `Mill.png` | The old watermill | A timber watermill with a turning waterwheel on a stream, mossy stone base, sacks of grain by the door |
| `RoadieCamp.png` | Roadie camp | A camp of heavy road cases, guitar amplifiers and a campfire under a canvas awning, ropes and cables everywhere |
| `QuestBearer.png` | The old skald | An old bearded skald in a hooded fur cloak, leaning on a carved staff, a harp on his back, standing by a small campfire |
| `RedTent.png` | The red tent | A large blood-red war tent with black skull banners blocking a road, two torches at its open flap, chains across the entrance |
| `ChainedMaiden.png` | The chained maiden (cut from the world map mockup) | A fierce warrior woman in bronze armour chained to a black stone pillar, chains glowing faintly, ash ground around her |
| `Oracle.png` | The bone oracle | A ring of huge dragon bones and a horned dragon skull on a dark hill, green smoke rising from inside the skull |
| `ExitPortal.png` | The Gate of Fire | A tall stone portal arch with demonic carvings, a vortex of fire inside it, lava cracks in the ground before it |
| `ArtefactCache.png` | An artefact on the ground | A weapon and a shield lying on a small rock, faint golden glow |
| `SpellShop.png` | Hall of Forbidden Hymns (spell shop) | A small dark stone temple with a domed roof, violet light in its arched door, spell books chained to lecterns outside, candles |
| `LuckShrine.png` | Fortune's Anvil (luck shrine) | A black anvil on a mossy stone circle, a hammer leaning on it, four-leaf clovers growing around, green sparks |
| `Minotaur.png` | Minotaur (map and battle figure, cut from the world map mockup) | A huge horned minotaur warrior with a double-headed axe, walking right; full figure, feet visible |

## Skill and spell icons (`Content/UI/Sprites/Skills/`, `Spells/`, 160 x 160, transparent)

Placeholder bronze medallions with an RPG Awesome glyph (`Tools/UiArt/build_magic.py`). Wanted: painted square icons in the
style of the artefact icons, one per secondary skill (Archery, Armorer, Offense, Diplomacy, Estates, Intelligence, Leadership,
Logistics, Luck, Mysticism, Pathfinding, Resistance, Sorcery, Tactics, Wisdom) and one per spell in `SpellDatabase.asset`. The
open spell book (`Content/UI/Sprites/SpellBook/SpellBook.png`, 1400 x 860) is a procedural leather book with two parchment pages:
wanted a painted open grimoire with the same page areas (the spell places sit on the pages).

## Units

| Where | Image | Prompt |
|---|---|---|
| `UnitDatabase.asset` > Berserker > Walk Frames | 8-frame right-facing walk cycle like the Roadie's | The Berserker of the town card: horned helm, red beard, huge axe, fur kilt |
| `UnitDatabase.asset` > Minotaur > Walk Frames | 8-frame right-facing walk cycle | As above, minotaur |

## Dialogue portraits (`DialogueDatabase.asset` > Speakers)

The old skald, the chained maiden and the bone oracle use their map tokens. Wanted: a square bust portrait of each in the style
of `Content/UI/Sprites/Gameplay/DialogueBox/LordVargSpeaker.png` (dark red background, warm key light).
