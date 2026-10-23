using System;
using System.Collections.Generic;
using System.IO;
using HeroesOfMetalAge.Dialogue;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off generation tool: it authored the expanded Ashen Pass. It painted the map prefab's terrain logic layer from
    /// <c>Editor/MapData/AshenPassTerrain.json</c> (derived from the painted image <c>Content/Maps/AshenPass/AshenPass.png</c> by
    /// the scripts in <c>Tools/MapLogic/</c>), gave the map a parent for its objects and rebuilt it; it wrote the mission's
    /// start, armies, map objects (gold, chest, colosseum, bastion, shrine, mill, roadie camp, quest giver, red tent, chained
    /// maiden, bone oracle, exit portal) and five goals into <c>AshenPass.asset</c>; it replaced the artefacts with the
    /// eleven-slot set drawn from <c>Content/UI/Sprites/Artefacts/</c>, added the minotaur, renamed the third recruit to
    /// Berserker, gave units, the hero and the speakers their portraits from the UI mockups and added the new conversations.
    /// It only acts while the mission has no goals, so it has nothing to do once it ran; everything it wrote is authored
    /// content edited in the Inspector from then on. The project works without it: delete this file, the
    /// <c>Editor/MapData/</c> folder and its call in <see cref="PrototypePipeline.Setup"/>, and the mentions in README.md and
    /// Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class MissionContentSeeder
    {
        private const string MAP_PREFAB = ContentAssets.MAPS_FOLDER + "/AshenPass/AshenPassMap.prefab";
        private const string TERRAIN_DATA = "Assets/HeroesOfMetalAge/Editor/MapData/AshenPassTerrain.json";
        private const string TILES = ContentAssets.MAPS_FOLDER + "/TerrainLogic";
        private const string ARTEFACT_ICONS = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Artefacts";
        private const string OBJECTS = "Assets/HeroesOfMetalAge/Content/World/MapObjects";
        private const string GAMEPLAY = "Assets/HeroesOfMetalAge/Content/UI/Sprites/Gameplay";
        private const int MAP_WIDTH = 100;
        private const string RED_KEY = "red key";

        [MenuItem("Tools/Heroes of Metal Age/Create the expanded Ashen Pass")]
        public static void CreateMissingAssets()
        {
            AdventureMapAuthoring map = AssetDatabase.LoadAssetAtPath<AdventureMapAuthoring>(MAP_PREFAB);
            if (map == null || ContentAssets.Mission.Objectives.Count > 0 || !File.Exists(TERRAIN_DATA))
            {
                return;
            }

            GameplayUiSeeder.ImportSprites();
            ImportArtefactIcons();
            if (map.Width != MAP_WIDTH)
            {
                PaintMap();
                AdventureMapBuilder.Rebuild(AssetDatabase.LoadAssetAtPath<AdventureMapAuthoring>(MAP_PREFAB));
            }

            AuthorArtefacts();
            AuthorUnits();
            AuthorHero();
            AuthorMission();
            AuthorDialogue();
            AuthorTheme();
            AssetDatabase.SaveAssets();
        }

        #region MAP
        /// <summary>The size of the terrain data; its rows of cells are read by <see cref="ParseCells"/>.</summary>
        [Serializable]
        private sealed class TerrainData
        {
            public int width;
            public int height;
        }

        private static void PaintMap()
        {
            string json = File.ReadAllText(TERRAIN_DATA);
            TerrainData data = JsonUtility.FromJson<TerrainData>(json);
            int width = data.width;
            int height = data.height;
            int[] terrain = ParseCells(json, width * height);

            Dictionary<TerrainType, TerrainTile> tiles = new Dictionary<TerrainType, TerrainTile>();
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainTile", new[] { TILES }))
            {
                TerrainTile tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(AssetDatabase.GUIDToAssetPath(guid));
                tiles[tile.Terrain] = tile;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(MAP_PREFAB);
            try
            {
                AdventureMapAuthoring authoring = root.GetComponent<AdventureMapAuthoring>();
                Tilemap logic = authoring.TerrainLogic;
                logic.ClearAllTiles();
                for (int row = 0; row < height; row++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        TerrainType type = (TerrainType)terrain[row * width + x];
                        if (type != TerrainType.Blocked && tiles.TryGetValue(type, out TerrainTile tile))
                        {
                            logic.SetTile(new Vector3Int(x, height - 1 - row, 0), tile);
                        }
                    }
                }

                logic.CompressBounds();
                AdventureMapView view = root.GetComponent<AdventureMapView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("mapObjects").objectReferenceValue == null)
                {
                    Transform enemies = (Transform)serialized.FindProperty("enemies").objectReferenceValue;
                    GameObject objects = new GameObject("Map Objects");
                    objects.transform.SetParent(enemies.parent, false);
                    objects.transform.SetSiblingIndex(enemies.GetSiblingIndex());
                    serialized.FindProperty("mapObjects").objectReferenceValue = objects.transform;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, MAP_PREFAB);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The terrain values of the "cells" rows, in order.</summary>
        private static int[] ParseCells(string json, int count)
        {
            int start = json.IndexOf("\"cells\"", StringComparison.Ordinal);
            int[] values = new int[count];
            int index = 0;
            int number = -1;
            for (int i = json.IndexOf('[', start) + 1; i < json.Length && index < count; i++)
            {
                char c = json[i];
                if (char.IsDigit(c))
                {
                    number = (number < 0 ? 0 : number * 10) + (c - '0');
                }
                else if (number >= 0)
                {
                    values[index++] = number;
                    number = -1;
                }
            }

            if (index != count)
            {
                throw new InvalidOperationException("The terrain data has " + index + " cells, expected " + count);
            }

            return values;
        }
        #endregion

        #region ARTEFACTS, UNITS, HERO
        private static void ImportArtefactIcons()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ARTEFACT_ICONS }))
            {
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.textureType == TextureImporterType.Sprite)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
            }
        }

        private readonly struct Artefact
        {
            public Artefact(string id, string name, string description, EquipmentSlot slot, ArtefactRarity rarity, string icon, int attack, int defense,
                int speed, int morale)
            {
                Id = id;
                Name = name;
                Description = description;
                Slot = slot;
                Rarity = rarity;
                Icon = icon;
                Attack = attack;
                Defense = defense;
                Speed = speed;
                Morale = morale;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public EquipmentSlot Slot { get; }
            public ArtefactRarity Rarity { get; }
            public string Icon { get; }
            public int Attack { get; }
            public int Defense { get; }
            public int Speed { get; }
            public int Morale { get; }
        }

        private static readonly Artefact[] Artefacts =
        {
            new Artefact("rusted-cleaver", "Rusted Cleaver", "A road-worn blade that still bites.", EquipmentSlot.Weapon, ArtefactRarity.Common, "common-right-hand", 1, 0, 0, 0),
            new Artefact("roadie-buckler", "Roadie's Buckler", "Riveted from a flight case lid.", EquipmentSlot.Shield, ArtefactRarity.Common, "common-left-hand", 0, 1, 0, 0),
            new Artefact("iron-skullcap", "Iron Skullcap", "Dented, honest and heavy.", EquipmentSlot.Head, ArtefactRarity.Common, "common-head", 0, 1, 0, 0),
            new Artefact("leather-jerkin", "Studded Jerkin", "Leather and iron rivets; it turns a glancing blow.", EquipmentSlot.Torso, ArtefactRarity.Common, "common-torso", 0, 1, 0, 0),
            new Artefact("stage-boots", "Stage Boots", "Made for stomping in time.", EquipmentSlot.Boots, ArtefactRarity.Common, "common-boots", 0, 0, 1, 0),
            new Artefact("ashen-ward", "Ashen Ward", "A charm bearing the clan's protective oath.", EquipmentSlot.Charm, ArtefactRarity.Common, "common-amulet", 0, 1, 0, 0),
            new Artefact("copper-ring", "Copper Knuckle Ring", "Every punch rings like a bell.", EquipmentSlot.Ring, ArtefactRarity.Common, "common-ring-1", 1, 0, 0, 0),
            new Artefact("studded-belt", "Bullet Belt", "The warhost cheers whoever wears it.", EquipmentSlot.Sash, ArtefactRarity.Common, "common-sash", 0, 0, 0, 1),
            new Artefact("fang-of-thunder", "Fang of Thunder", "The wardens' weapon, tempered in thunder.", EquipmentSlot.Weapon, ArtefactRarity.Rare, "rare-right-hand", 2, 0, 0, 0),
            new Artefact("warden-kite-shield", "Warden's Kite Shield", "Blue steel that has held the pass for a century.", EquipmentSlot.Shield, ArtefactRarity.Rare, "rare-left-hand", 0, 2, 0, 0),
            new Artefact("sapphire-helm", "Sapphire Helm", "Its visor glows when danger nears.", EquipmentSlot.Head, ArtefactRarity.Rare, "rare-head", 1, 1, 0, 0),
            new Artefact("blue-steel-cuirass", "Blue Steel Cuirass", "Cold to the touch, harder than the cold.", EquipmentSlot.Torso, ArtefactRarity.Rare, "rare-torso", 0, 2, 0, 0),
            new Artefact("ring-of-feedback", "Ring of Feedback", "It hums a note no one else hears.", EquipmentSlot.Ring, ArtefactRarity.Rare, "rare-ring-2", 1, 0, 1, 0),
            new Artefact("warborn-sigil", "Warborn Sigil", "The Black Banner's mark of relentless advance.", EquipmentSlot.Charm, ArtefactRarity.Rare, "rare-amulet", 0, 0, 1, 0),
            new Artefact("riders-greaves", "Rider's Greaves", "Spurred and silver-lined.", EquipmentSlot.Boots, ArtefactRarity.Rare, "rare-boots", 0, 1, 1, 0),
            new Artefact("winged-blade", "Winged Blade", "Amethyst in the hilt, wings on the guard, murder on the edge.", EquipmentSlot.Weapon, ArtefactRarity.Epic, "epic-right-hand", 3, 0, 0, 0),
            new Artefact("violet-aegis", "Violet Aegis", "Gold inlay sings when struck.", EquipmentSlot.Shield, ArtefactRarity.Epic, "epic-left-hand", 0, 3, 0, 0),
            new Artefact("war-crown", "Amethyst War Crown", "Worn by the first chieftain who sang to battle.", EquipmentSlot.Head, ArtefactRarity.Epic, "epic-head", 2, 0, 0, 1),
            new Artefact("ring-of-the-horned-god", "Ring of the Horned God", "Raise your fist and the warhost roars.", EquipmentSlot.Ring, ArtefactRarity.Epic, "epic-ring-1", 1, 0, 0, 1),
            new Artefact("sash-of-the-pit", "Sash of the Pit", "Tied by those who survived the circle.", EquipmentSlot.Sash, ArtefactRarity.Epic, "epic-sash", 0, 2, 0, 0),
            new Artefact("chain-of-the-iron-maiden", "Chain of the Iron Maiden", "The chain that bound her, turned into a vow.", EquipmentSlot.Necklace, ArtefactRarity.Epic, "epic-amulet", 2, 2, 0, 1),
            new Artefact("axe-of-the-first-riff", "Axe of the First Riff", "Forged when the first chord split the sky. Its edge still hums.", EquipmentSlot.Weapon, ArtefactRarity.Legendary, "legendary-right-hand", 4, 0, 1, 0),
            new Artefact("amulet-of-endless-feedback", "Amulet of Endless Feedback", "A shard of a burning amplifier that never stops ringing.", EquipmentSlot.Charm, ArtefactRarity.Legendary, "legendary-amulet", 1, 3, 1, 0),
            new Artefact("dragonscale-plate", "Dragonscale Plate", "Shed by something that still sleeps.", EquipmentSlot.Torso, ArtefactRarity.Legendary, "legendary-torso", 0, 4, 0, 0),
            new Artefact("phoenix-helm", "Phoenix Helm", "Ember gems that never cool.", EquipmentSlot.Head, ArtefactRarity.Legendary, "legendary-head", 2, 2, 0, 0),
            new Artefact("ember-ring", "Ember Ring", "Burns the hand that is not worthy.", EquipmentSlot.Ring, ArtefactRarity.Legendary, "legendary-ring-1", 2, 0, 0, 1),
            new Artefact("obsidian-greaves", "Obsidian Greaves", "Each step leaves a scorched print.", EquipmentSlot.Boots, ArtefactRarity.Legendary, "legendary-boots", 0, 2, 2, 0),
            new Artefact("gloves-of-metal", "Gloves of Metal", "The gauntlets of the first metal king. Whoever wears them never drops the riff.", EquipmentSlot.Gloves, ArtefactRarity.Legendary, "legendary-gloves", 3, 2, 0, 1),
        };

        private static void AuthorArtefacts()
        {
            SerializedObject database = new SerializedObject(ContentAssets.Artefacts);
            SerializedProperty list = database.FindProperty("artefacts");
            list.arraySize = Artefacts.Length;
            for (int i = 0; i < Artefacts.Length; i++)
            {
                Artefact artefact = Artefacts[i];
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("id").stringValue = artefact.Id;
                entry.FindPropertyRelative("displayName").stringValue = artefact.Name;
                entry.FindPropertyRelative("description").stringValue = artefact.Description;
                entry.FindPropertyRelative("icon").objectReferenceValue = Load<Sprite>(ARTEFACT_ICONS + "/" + artefact.Icon + ".png");
                entry.FindPropertyRelative("slot").intValue = (int)artefact.Slot;
                entry.FindPropertyRelative("rarity").intValue = (int)artefact.Rarity;
                entry.FindPropertyRelative("attackBonus").intValue = artefact.Attack;
                entry.FindPropertyRelative("defenseBonus").intValue = artefact.Defense;
                entry.FindPropertyRelative("speedBonus").intValue = artefact.Speed;
                entry.FindPropertyRelative("moraleBonus").intValue = artefact.Morale;
            }

            database.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AuthorUnits()
        {
            SerializedObject database = new SerializedObject(ContentAssets.Units);
            SerializedProperty list = database.FindProperty("units");
            SetUnitCard(list, "ironbound", Load<Sprite>(GAMEPLAY + "/Town/RoadieCard.png"));
            SetUnitCard(list, "ash-archer", Load<Sprite>(GAMEPLAY + "/Town/AirGuitaristCard.png"));
            SerializedProperty berserker = SetUnitCard(list, "doom-rider", Load<Sprite>(GAMEPLAY + "/Town/BerserkerCard.png"));
            berserker.FindPropertyRelative("displayName").stringValue = "BERSERKER";
            berserker.FindPropertyRelative("description").stringValue = "Frenzied melee powerhouse, fearsome in close combat.";
            berserker.FindPropertyRelative("portrait").objectReferenceValue = Load<Sprite>(OBJECTS + "/BerserkerFigure.png");

            SerializedProperty minotaur = Find(list, "minotaur");
            if (minotaur == null)
            {
                list.arraySize++;
                minotaur = list.GetArrayElementAtIndex(list.arraySize - 1);
                minotaur.FindPropertyRelative("walkFrames").arraySize = 0;
                minotaur.FindPropertyRelative("voice").objectReferenceValue = null;
            }

            Sprite figure = Load<Sprite>(OBJECTS + "/Minotaur.png");
            minotaur.FindPropertyRelative("id").stringValue = "minotaur";
            minotaur.FindPropertyRelative("displayName").stringValue = "MINOTAUR";
            minotaur.FindPropertyRelative("description").stringValue = "Horned brutes of the ash wastes; they guard what they do not understand.";
            minotaur.FindPropertyRelative("portrait").objectReferenceValue = figure;
            minotaur.FindPropertyRelative("cardPortrait").objectReferenceValue = figure;
            minotaur.FindPropertyRelative("placeholderIcon").stringValue = "axe";
            minotaur.FindPropertyRelative("health").intValue = 60;
            minotaur.FindPropertyRelative("minimumDamage").intValue = 9;
            minotaur.FindPropertyRelative("maximumDamage").intValue = 15;
            minotaur.FindPropertyRelative("attack").intValue = 11;
            minotaur.FindPropertyRelative("defense").intValue = 9;
            minotaur.FindPropertyRelative("speed").intValue = 6;
            minotaur.FindPropertyRelative("recruitmentCost").intValue = 450;
            minotaur.FindPropertyRelative("weeklyGrowth").intValue = 0;
            minotaur.FindPropertyRelative("ranged").boolValue = false;
            minotaur.FindPropertyRelative("voice").objectReferenceValue = Load<MLin.Audio.SoundDefinition>("Assets/HeroesOfMetalAge/Content/Audio/Sounds/VoiceMinotaur.asset");
            database.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty SetUnitCard(SerializedProperty list, string id, Sprite card)
        {
            SerializedProperty unit = Find(list, id) ?? throw new InvalidOperationException("Unit " + id + " is missing.");
            unit.FindPropertyRelative("cardPortrait").objectReferenceValue = card;
            return unit;
        }

        private static void AuthorHero()
        {
            SerializedObject database = new SerializedObject(ContentAssets.Heroes);
            SerializedProperty kael = Find(database.FindProperty("heroes"), "kael") ?? throw new InvalidOperationException("Hero kael is missing.");
            kael.FindPropertyRelative("panelPortrait").objectReferenceValue = Load<Sprite>(GAMEPLAY + "/Adventure/KaelPortrait.png");
            kael.FindPropertyRelative("screenPortrait").objectReferenceValue = Load<Sprite>(GAMEPLAY + "/Hero/KaelRider.png");
            database.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region MISSION
        private readonly struct Army
        {
            public Army(string unitId, int count)
            {
                UnitId = unitId;
                Count = count;
            }

            public string UnitId { get; }
            public int Count { get; }
        }

        private static void AuthorMission()
        {
            MissionSettingsScriptableObject mission = ContentAssets.Mission;
            SerializedObject serialized = new SerializedObject(mission);
            serialized.FindProperty("startingPosition").vector2IntValue = new Vector2Int(3, 30);
            serialized.FindProperty("description").stringValue = "Across the pass, the Ashen Clan sells steel. Beyond its castle the Black Banner holds the "
                + "gate of fire, and in the burning east a maiden waits in chains.";
            serialized.FindProperty("townPortrait").objectReferenceValue = Load<Sprite>(GAMEPLAY + "/Town/TownPicture.png");
            serialized.FindProperty("moraleChance").floatValue = 0.1f;
            serialized.FindProperty("epicChance").floatValue = 0.15f;
            SerializedProperty pages = serialized.FindProperty("briefingPages");
            if (pages.arraySize == 2)
            {
                pages.arraySize = 3;
                pages.GetArrayElementAtIndex(2).stringValue = "Beyond the clan's castle the Black Banner holds the gate of fire. In the burning east a maiden "
                    + "is chained among horned beasts, and under the mountains something old is sleeping.";
            }

            Sprite greenVale = BattleImage("GreenVale");
            Sprite infernal = BattleImage("InfernalWaste");
            SerializedProperty encounters = serialized.FindProperty("encounters");
            encounters.arraySize = 0;
            Encounter(encounters, "willing", "THE WILLING", EncounterKind.Friendly, 7, 26, "We heard the iron oath. Let us ride with you.", 0, 0, null,
                greenVale, "friendly-arrival", null, new Army("ash-archer", 6));
            Encounter(encounters, "road-crew", "THE ROAD CREW", EncounterKind.Friendly, 41, 42, "Your amps are loud, your cause is louder. We carry your gear.",
                0, 0, null, greenVale, null, null, new Army("ironbound", 8));
            Encounter(encounters, "bandits", "FOREST BANDITS", EncounterKind.Sentinel, 30, 36, "Cutthroats who sell what they steal from the road.", 150, 300,
                null, greenVale, null, null, new Army("ironbound", 9), new Army("ash-archer", 4));
            Encounter(encounters, "iron-reavers", "THE IRON REAVERS", EncounterKind.Sentinel, 45, 14,
                "Raiders camped on a buried treasure. They swear it is cursed and will not leave it.", 300, 400, "gloves-of-metal", greenVale, null, null,
                new Army("doom-rider", 3), new Army("ironbound", 10));
            Encounter(encounters, "sentinel", "THE PASS WARDENS", EncounterKind.Sentinel, 58, 29,
                "The wardens block the only passage through the ridge and refuse to join.", 200, 500, null, greenVale, "sentinel-arrival", null,
                new Army("ironbound", 14), new Army("ash-archer", 6));
            Encounter(encounters, "town", "ASHEN HOLD", EncounterKind.Town, 72, 27,
                "The Ashen Clan will not surrender its ancestral forges. Defeat its defenders to claim the town.", 300, 400, null, infernal, "town-arrival", null,
                new Army("ironbound", 16), new Army("ash-archer", 8));
            Encounter(encounters, "revenants", "ASH REVENANTS", EncounterKind.Sentinel, 80, 37, "The dead of the ash wastes still march.", 300, 500, null,
                infernal, null, null, new Army("doom-rider", 5), new Army("ash-archer", 10));
            Encounter(encounters, "minotaurs", "THE MINOTAUR PACK", EncounterKind.Sentinel, 87, 48, "Horned brutes guard the chained maiden.", 450, 600, null,
                infernal, null, Load<Sprite>(OBJECTS + "/Minotaur.png"), new Army("minotaur", 6));
            Encounter(encounters, "boss", "THE BLACK BANNER", EncounterKind.Boss, 85, 30,
                "The enemy beyond the castle holds the gate of fire. Recruit and equip your best before challenging them.", 600, 1200, null, infernal,
                "boss-arrival", null, new Army("ironbound", 24), new Army("ash-archer", 14), new Army("doom-rider", 6));

            SerializedProperty objects = serialized.FindProperty("mapObjects");
            objects.arraySize = 0;
            MapObject(objects, "spilled-purse", "A SPILLED PURSE", MapObjectKind.GoldPile, 11, 29, "Someone fled in a hurry.", "GoldPile", 0.8f, gold: 400);
            MapObject(objects, "bandit-hoard", "THE BANDITS' HOARD", MapObjectKind.GoldPile, 20, 46, "Hidden under a fallen pine.", "GoldPile", 0.8f, gold: 600);
            MapObject(objects, "ash-tribute", "A TRIBUTE IN ASH", MapObjectKind.GoldPile, 79, 32, "Coins left for gods who stopped listening.", "GoldPile", 0.8f,
                gold: 800);
            MapObject(objects, "old-chest", "AN OLD CHEST", MapObjectKind.TreasureChest, 21, 18, "Iron bands, a broken lock and the glint of gold.", "Chest", 1f,
                gold: 1000, experience: 500);
            MapObject(objects, "lost-buckler", "A LOST BUCKLER", MapObjectKind.Artefact, 20, 30, "A shield lies in the grass.", "ArtefactCache", 0.9f,
                artefactId: "roadie-buckler");
            MapObject(objects, "horned-ring", "A RING IN THE ASH", MapObjectKind.Artefact, 74, 44, "A ring glows in the ash.", "ArtefactCache", 0.9f,
                artefactId: "ring-of-the-horned-god");
            MapObject(objects, "colosseum", "THE BLOODY COLOSSEUM", MapObjectKind.AttackShrine, 11, 8, "The old arena still teaches the sword.", "Colosseum", 1.3f,
                attack: 2);
            MapObject(objects, "iron-bastion", "THE IRON BASTION", MapObjectKind.DefenseShrine, 16, 37, "Its veterans teach you to hold the line.", "IronBastion",
                1.3f, defense: 2);
            MapObject(objects, "shrine-of-horns", "SHRINE OF THE HORNS", MapObjectKind.MoraleShrine, 37, 29, "Raise the horns and the warhost roars.",
                "ShrineOfHorns", 1.2f, morale: 2);
            MapObject(objects, "watermill", "THE OLD WATERMILL", MapObjectKind.Mill, 32, 6, "The miller pays his tithe to whoever protects the road.", "Mill", 1.2f,
                gold: 750);
            MapObject(objects, "roadie-camp", "ROADIE CAMP", MapObjectKind.Dwelling, 29, 54, "Roadies between tours, looking for work.", "RoadieCamp", 1.2f,
                unitId: "ironbound", weeklyGrowth: 8);
            MapObject(objects, "old-skald", "THE OLD SKALD", MapObjectKind.QuestGiver, 8, 51, "The skald has told you all he knows.", "QuestBearer", 1.1f,
                keyId: RED_KEY, conversation: "quest-bearer");
            MapObject(objects, "red-tent", "THE RED TENT", MapObjectKind.BorderGate, 85, 48, "The keepers of the red tent let only the bearer of their key pass.",
                "RedTent", 1.2f, keyId: RED_KEY, conversation: "red-tent");
            MapObject(objects, "chained-maiden", "THE CHAINED MAIDEN", MapObjectKind.Prison, 90, 48, "The halls of femdom had been restored.", "ChainedMaiden", 1.4f,
                artefactId: "chain-of-the-iron-maiden", conversation: "maiden-freed");
            MapObject(objects, "bone-oracle", "THE BONE ORACLE", MapObjectKind.Oracle, 89, 15, "The bones have told you of the sleeping dragon.", "Oracle", 1.2f,
                conversation: "dragon-lore");
            MapObject(objects, "gate-of-fire", "THE GATE OF FIRE", MapObjectKind.ExitPortal, 89, 30, "The gate of fire burns open.", "ExitPortal", 1.6f);

            SerializedProperty goals = serialized.FindProperty("objectives");
            goals.arraySize = 0;
            Goal(goals, "defeat-black-banner", "Defeat the Black Banner", ObjectiveKind.DefeatEncounter, "boss", 0, true);
            Goal(goals, "free-the-maiden", "Free the chained maiden", ObjectiveKind.VisitObject, "chained-maiden", 0, false);
            Goal(goals, "gloves-of-metal", "Find the Gloves of Metal", ObjectiveKind.OwnArtefact, "gloves-of-metal", 0, false);
            Goal(goals, "sleeping-dragon", "Learn of the sleeping dragon", ObjectiveKind.VisitObject, "bone-oracle", 0, false);
            Goal(goals, "level-five", "Reach level 5", ObjectiveKind.ReachLevel, null, 5, false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Encounter(SerializedProperty list, string id, string name, EncounterKind kind, int x, int y, string description, int experience,
            int gold, string artefactId, Sprite battleground, string conversation, Sprite mapSprite, params Army[] army)
        {
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("position").vector2IntValue = new Vector2Int(x, y);
            entry.FindPropertyRelative("description").stringValue = description;
            entry.FindPropertyRelative("rewardExperience").intValue = experience;
            entry.FindPropertyRelative("rewardGold").intValue = gold;
            entry.FindPropertyRelative("rewardArtefactId").stringValue = artefactId ?? string.Empty;
            entry.FindPropertyRelative("battleBackground").objectReferenceValue = battleground;
            entry.FindPropertyRelative("mapSprite").objectReferenceValue = mapSprite;
            entry.FindPropertyRelative("arrivalConversationId").stringValue = conversation ?? string.Empty;
            SerializedProperty troops = entry.FindPropertyRelative("army");
            troops.arraySize = army.Length;
            for (int i = 0; i < army.Length; i++)
            {
                troops.GetArrayElementAtIndex(i).FindPropertyRelative("unitId").stringValue = army[i].UnitId;
                troops.GetArrayElementAtIndex(i).FindPropertyRelative("count").intValue = army[i].Count;
            }
        }

        private static void MapObject(SerializedProperty list, string id, string name, MapObjectKind kind, int x, int y, string description, string sprite,
            float height, int gold = 0, int experience = 0, string artefactId = null, int attack = 0, int defense = 0, int morale = 0, string unitId = null,
            int weeklyGrowth = 0, string keyId = null, string conversation = null)
        {
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("position").vector2IntValue = new Vector2Int(x, y);
            entry.FindPropertyRelative("description").stringValue = description;
            entry.FindPropertyRelative("sprite").objectReferenceValue = Load<Sprite>(OBJECTS + "/" + sprite + ".png");
            entry.FindPropertyRelative("mapHeight").floatValue = height;
            entry.FindPropertyRelative("gold").intValue = gold;
            entry.FindPropertyRelative("experience").intValue = experience;
            entry.FindPropertyRelative("artefactId").stringValue = artefactId ?? string.Empty;
            entry.FindPropertyRelative("attackBonus").intValue = attack;
            entry.FindPropertyRelative("defenseBonus").intValue = defense;
            entry.FindPropertyRelative("moraleBonus").intValue = morale;
            entry.FindPropertyRelative("unitId").stringValue = unitId ?? string.Empty;
            entry.FindPropertyRelative("weeklyGrowth").intValue = weeklyGrowth;
            entry.FindPropertyRelative("keyId").stringValue = keyId ?? string.Empty;
            entry.FindPropertyRelative("arrivalConversationId").stringValue = conversation ?? string.Empty;
        }

        private static void Goal(SerializedProperty list, string id, string text, ObjectiveKind kind, string targetId, int value, bool isRequired)
        {
            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("text").stringValue = text;
            entry.FindPropertyRelative("kind").intValue = (int)kind;
            entry.FindPropertyRelative("targetId").stringValue = targetId ?? string.Empty;
            entry.FindPropertyRelative("value").intValue = value;
            entry.FindPropertyRelative("isRequired").boolValue = isRequired;
        }

        private static Sprite BattleImage(string name)
        {
            return Load<Sprite>("Assets/HeroesOfMetalAge/Content/World/Battlefields/" + name + ".png");
        }
        #endregion

        #region DIALOGUE AND THEME
        private static void AuthorDialogue()
        {
            DialogueDatabaseScriptableObject dialogue = ContentAssets.Dialogue;
            SerializedObject serialized = new SerializedObject(dialogue);
            SerializedProperty speakers = serialized.FindProperty("speakers");
            Speaker(speakers, "kael", "KAEL IRONSONG", Load<Sprite>(GAMEPLAY + "/Adventure/KaelPortrait.png"));
            Speaker(speakers, "lord-varg", "LORD VARG", Load<Sprite>(GAMEPLAY + "/DialogueBox/LordVargSpeaker.png"));
            Speaker(speakers, "air-guitarists", "AIR GUITARISTS", Load<Sprite>(GAMEPLAY + "/DialogueBox/AirGuitaristSpeaker.png"));
            Speaker(speakers, "pass-wardens", "PASS WARDENS", Load<Sprite>(GAMEPLAY + "/Town/RoadieCard.png"));
            Speaker(speakers, "old-skald", "THE OLD SKALD", Load<Sprite>(OBJECTS + "/QuestBearer.png"));
            Speaker(speakers, "chained-maiden", "THE CHAINED MAIDEN", Load<Sprite>(OBJECTS + "/ChainedMaiden.png"));
            Speaker(speakers, "bone-oracle", "THE BONE ORACLE", Load<Sprite>(OBJECTS + "/Oracle.png"));

            SerializedProperty conversations = serialized.FindProperty("conversations");
            Conversation(conversations, "quest-bearer",
                ("old-skald", "Hail, warlord of the iron oath. Hear an old man before you ride east."),
                ("old-skald", "Beyond the clan's castle, past the red tent, horned beasts keep a maiden in chains."),
                ("old-skald", "Take this red key. The keepers of the tent open only for its bearer."),
                ("kael", "Then I ride for her."));
            Conversation(conversations, "red-tent",
                ("kael", "A red tent bars the way. Somewhere in the green lands someone must hold its key."));
            Conversation(conversations, "maiden-freed",
                ("chained-maiden", "You broke my chains, rider of the oath."),
                ("chained-maiden", "The halls of femdom had been restored. Take the chain that bound me; let it bind your enemies."),
                ("kael", "I will wear it with honour."));
            Conversation(conversations, "dragon-lore",
                ("bone-oracle", "Bones remember what the living forget."),
                ("bone-oracle", "Under the burning mountains a dragon sleeps. When the Secret of Metal is sung, it will wake."),
                ("kael", "Let it sleep. For now."));
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Speaker(SerializedProperty list, string id, string name, Sprite portrait)
        {
            SerializedProperty entry = Find(list, id);
            if (entry == null)
            {
                list.arraySize++;
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative("id").stringValue = id;
            }

            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("portrait").objectReferenceValue = portrait;
        }

        private static void Conversation(SerializedProperty list, string id, params (string Speaker, string Text)[] lines)
        {
            if (Find(list, id) != null)
            {
                return;
            }

            list.arraySize++;
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("id").stringValue = id;
            entry.FindPropertyRelative("style").intValue = (int)DialogueStyle.PortraitBox;
            SerializedProperty entries = entry.FindPropertyRelative("lines");
            entries.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++)
            {
                SerializedProperty line = entries.GetArrayElementAtIndex(i);
                line.FindPropertyRelative("id").stringValue = id + "-" + (i + 1).ToString("00");
                line.FindPropertyRelative("speakerId").stringValue = lines[i].Speaker;
                line.FindPropertyRelative("text").stringValue = lines[i].Text;
                line.FindPropertyRelative("voiceOver").objectReferenceValue = null;
            }
        }

        private static void AuthorTheme()
        {
            SerializedObject theme = new SerializedObject(ContentAssets.Theme);
            theme.FindProperty("checkMark").objectReferenceValue = Load<Sprite>(GAMEPLAY + "/Common/CheckMark.png");
            theme.FindProperty("mapObject").objectReferenceValue = Load<Sprite>(OBJECTS + "/ArtefactCache.png");
            theme.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        private static SerializedProperty Find(SerializedProperty list, string id)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue == id)
                {
                    return entry;
                }
            }

            return null;
        }

        private static T Load<T>(string path)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing asset " + path);
            }

            return asset;
        }
    }
}
