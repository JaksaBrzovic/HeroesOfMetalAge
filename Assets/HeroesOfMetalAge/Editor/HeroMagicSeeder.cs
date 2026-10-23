using System;
using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Presentation;
using MLin.Audio;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// One-off generation tool for hero growth and magic. It created the hero rules (<c>HeroRules.asset</c>: experience,
    /// secondary skills, luck, mana, Diplomacy and surrender), the hero class database with the Warrior, the spell database,
    /// the spell cast and luck sounds, gave Kael the Warrior class and his starting skills, placed the spell shop and the luck
    /// shrine on the Ashen Pass and gave the clan's town and the Black Banner their commanders; and in the UI it created the
    /// <c>IconRow</c> and <c>SpellSlot</c> widgets and the <c>LevelUp</c>, <c>SpellBook</c> and <c>SpellShop</c> screens, nested
    /// them into GameUiRoot, and added the skills tab, the magic stats and the spell book button to the Hero screen and the
    /// spell book and surrender buttons to the Combat screen. The pictures and sounds come from <c>Tools/UiArt/build_magic.py</c>.
    /// Every step only runs while what it creates is missing, so the tool does nothing once it ran and the assets are edited by
    /// hand from then on. The project works without it: delete this file and its call in <see cref="PrototypePipeline.Setup"/>,
    /// and the mentions in README.md and Docs/HOMA_ARCHITECTURE.md.
    /// </summary>
    public static class HeroMagicSeeder
    {
        private const string SPRITES = "Assets/HeroesOfMetalAge/Content/UI/Sprites";
        private const string OBJECT_SPRITES = "Assets/HeroesOfMetalAge/Content/World/MapObjects";
        private const string SOUNDS = ContentAssets.AUDIO_FOLDER + "/Sounds";
        private const string WIDGETS = ContentAssets.UI_FOLDER + "/Widgets";
        private const string ICON_ROW_PATH = WIDGETS + "/IconRow.prefab";
        private const string SPELL_SLOT_PATH = WIDGETS + "/SpellSlot.prefab";
        private const string HERO_ID = "kael";
        private const float AUTO_SIZE_MINIMUM = 0.55f;

        private static readonly Color TextColor = new Color(0.93f, 0.89f, 0.8f, 1f);
        private static readonly Color GoldColor = new Color(0.93f, 0.76f, 0.45f, 1f);
        private static readonly Color InkColor = new Color(0.2f, 0.12f, 0.05f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.6f);
        private static readonly Color PanelColor = new Color(0.05f, 0.04f, 0.035f, 0.96f);
        private static readonly Color CardColor = new Color(0.1f, 0.08f, 0.06f, 0.9f);

        [MenuItem("Tools/Heroes of Metal Age/Create missing hero growth and magic assets")]
        public static void CreateMissingAssets()
        {
            ImportArt();
            CreateHeroRules();
            bool isNewClassDatabase = CreateClasses();
            CreateSpells();
            if (isNewClassDatabase)
            {
                SeedHero();
            }

            CreateSounds();
            SeedMission();
            TypographySettings typography = TypographyTools.Settings;
            if (typography == null || ContentAssets.RootPrefab == null)
            {
                AssetDatabase.SaveAssets();
                return;
            }

            GameObject iconRow = CreateIconRow(typography);
            GameObject spellSlot = CreateSpellSlot(typography);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.LEVEL_UP_PATH) == null)
            {
                BuildLevelUp(typography);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.SPELL_BOOK_PATH) == null)
            {
                BuildSpellBook(typography, spellSlot);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ContentAssets.SPELL_SHOP_PATH) == null)
            {
                BuildSpellShop(typography, iconRow);
            }

            ExtendHeroScreen(typography, iconRow);
            ExtendCombatScreen();
            NestIntoRoot();
            AssetDatabase.SaveAssets();
        }

        #region CONTENT
        private static void ImportArt()
        {
            GameplayUiSeeder.ImportSprites();
            foreach (string folder in new[] { SPRITES + "/Skills", SPRITES + "/Spells", SPRITES + "/SpellBook" })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (importer.textureType == TextureImporterType.Sprite)
                    {
                        continue;
                    }

                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    importer.maxTextureSize = 2048;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void CreateHeroRules()
        {
            if (ContentAssets.HeroRules != null)
            {
                return;
            }

            HeroRulesScriptableObject rules = ScriptableObject.CreateInstance<HeroRulesScriptableObject>();
            foreach (SkillEntry skill in rules.Skills)
            {
                skill.Icon = LoadSprite(SPRITES + "/Skills/" + skill.Skill + ".png");
            }

            AssetDatabase.CreateAsset(rules, ContentAssets.HERO_RULES_PATH);
        }

        /// <returns>True when the database was created now.</returns>
        private static bool CreateClasses()
        {
            if (ContentAssets.HeroClasses != null)
            {
                return false;
            }

            // the weights of the classic knight for the skills a warrior may learn
            SkillWeightEntry[] weights =
            {
                new SkillWeightEntry(SecondarySkill.Archery, 5), new SkillWeightEntry(SecondarySkill.Armorer, 5), new SkillWeightEntry(SecondarySkill.Offense, 7),
                new SkillWeightEntry(SecondarySkill.Diplomacy, 4), new SkillWeightEntry(SecondarySkill.Estates, 6),
                new SkillWeightEntry(SecondarySkill.Intelligence, 1), new SkillWeightEntry(SecondarySkill.Leadership, 10),
                new SkillWeightEntry(SecondarySkill.Logistics, 5), new SkillWeightEntry(SecondarySkill.Luck, 3), new SkillWeightEntry(SecondarySkill.Mysticism, 2),
                new SkillWeightEntry(SecondarySkill.Pathfinding, 4), new SkillWeightEntry(SecondarySkill.Resistance, 5), new SkillWeightEntry(SecondarySkill.Sorcery, 1),
                new SkillWeightEntry(SecondarySkill.Tactics, 7), new SkillWeightEntry(SecondarySkill.Wisdom, 3)
            };
            HeroClassDatabaseScriptableObject classes = ScriptableObject.CreateInstance<HeroClassDatabaseScriptableObject>();
            classes.SetClasses(new[]
            {
                new HeroClassEntry("warrior", "Warrior", "A fighter first: attack and defense grow fastest, magic comes slowly.", weights)
            });
            AssetDatabase.CreateAsset(classes, ContentAssets.HERO_CLASSES_PATH);
            return true;
        }

        private static void CreateSpells()
        {
            if (ContentAssets.Spells != null)
            {
                return;
            }

            SpellEntry[] spells =
            {
                new SpellEntry("magic-arrow", "Magic Arrow", "A bolt of raw force strikes one enemy stack.", 1, 5, 150, SpellTarget.Enemy, SpellEffect.Damage, 10, 10, 0, 0),
                new SpellEntry("cure", "Cure", "Closes the wounds of the top creature of one of your stacks.", 1, 6, 150, SpellTarget.Ally, SpellEffect.Heal, 10, 5, 0, 0),
                new SpellEntry("bless", "Bless", "Every blow of the stack lands with its full weight.", 1, 5, 150, SpellTarget.Ally, SpellEffect.Bless, 0, 0, 0, 1),
                new SpellEntry("curse", "Curse", "The enemy stack strikes as weakly as it can.", 1, 6, 150, SpellTarget.Enemy, SpellEffect.Curse, 0, 0, 0, 1),
                new SpellEntry("haste", "Haste", "A drumbeat quickens the stack's feet.", 1, 6, 150, SpellTarget.Ally, SpellEffect.Speed, 3, 0, 0, 1),
                new SpellEntry("slow", "Slow", "The enemy stack wades as if through tar.", 1, 6, 150, SpellTarget.Enemy, SpellEffect.Speed, -2, 0, 0, 1),
                new SpellEntry("bloodlust", "Bloodlust", "Battle fury raises the stack's attack.", 1, 5, 150, SpellTarget.Ally, SpellEffect.Attack, 3, 0, 0, 1),
                new SpellEntry("stone-skin", "Stone Skin", "The stack's skin hardens like granite.", 1, 5, 150, SpellTarget.Ally, SpellEffect.Defense, 3, 0, 0, 1),
                new SpellEntry("shield", "Shield", "An unseen shield turns aside part of every melee blow.", 1, 5, 150, SpellTarget.Ally, SpellEffect.Shield, 15, 0, 0, 1),
                new SpellEntry("lightning-bolt", "Lightning Bolt", "Thunder from a clear sky strikes one enemy stack.", 2, 10, 300, SpellTarget.Enemy, SpellEffect.Damage, 10, 25, 0, 0),
                new SpellEntry("weakness", "Weakness", "The enemy stack's arms grow heavy.", 2, 8, 300, SpellTarget.Enemy, SpellEffect.Attack, -3, 0, 0, 1),
                new SpellEntry("fireball", "Fireball", "A ball of fire bursts over a hex and burns every stack around it.", 3, 15, 600, SpellTarget.Area, SpellEffect.Damage, 15, 10, 0, 0),
                new SpellEntry("battle-hymn", "Battle Hymn", "A war song lifts the attack of your whole warhost.", 4, 16, 1000, SpellTarget.AllAllies, SpellEffect.Attack, 2, 0, 0, 1),
                new SpellEntry("meteor-shower", "Meteor Shower", "Rocks of fire fall on a hex and everything around it.", 4, 16, 1000, SpellTarget.Area, SpellEffect.Damage, 25, 25, 0, 0),
                new SpellEntry("implosion", "Implosion", "One enemy stack is crushed from within.", 5, 30, 1600, SpellTarget.Enemy, SpellEffect.Damage, 100, 75, 0, 0),
                new SpellEntry("armageddon", "Armageddon", "Fire rains on the whole battlefield, friend and foe alike.", 5, 24, 1600, SpellTarget.AllStacks, SpellEffect.Damage, 30, 50, 0, 0)
            };
            foreach (SpellEntry spell in spells)
            {
                spell.Icon = LoadSprite(SPRITES + "/Spells/" + spell.Id + ".png");
            }

            SpellDatabaseScriptableObject database = ScriptableObject.CreateInstance<SpellDatabaseScriptableObject>();
            database.SetSpells(spells);
            AssetDatabase.CreateAsset(database, ContentAssets.SPELLS_PATH);
        }

        /// <summary>Kael becomes a Warrior with Basic Leadership and Basic Offense, as the classic knights start.</summary>
        private static void SeedHero()
        {
            HeroDataBaseScriptableObject heroes = ContentAssets.Heroes;
            HeroEntry hero = heroes.Heroes.FirstOrDefault(entry => entry.Id == HERO_ID);
            if (hero == null)
            {
                return;
            }

            hero.ClassId = "warrior";
            hero.SetStartingSkills(new[] { new SkillLevelEntry(SecondarySkill.Leadership, SkillLevel.Basic), new SkillLevelEntry(SecondarySkill.Offense, SkillLevel.Basic) });
            EditorUtility.SetDirty(heroes);
        }

        private static void CreateSounds()
        {
            AudioDatabaseScriptableObject audio = ContentAssets.Audio;
            SerializedObject serialized = new SerializedObject(audio);
            bool isChanged = false;
            foreach ((string field, string asset, string clip) in new[]
            {
                ("spellCast", "SpellCast", "spell_cast.wav"), ("goodLuck", "GoodLuck", "good_luck.wav"), ("badLuck", "BadLuck", "bad_luck.wav")
            })
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property.objectReferenceValue != null)
                {
                    continue;
                }

                string path = SOUNDS + "/" + asset + ".asset";
                SoundDefinition sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
                if (sound == null)
                {
                    // the blessing's settings: the effects channel, a moderate volume and two voices at most
                    sound = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SoundDefinition>(SOUNDS + "/Blessing.asset"));
                    SerializedObject definition = new SerializedObject(sound);
                    SerializedProperty clips = definition.FindProperty("clips");
                    clips.arraySize = 1;
                    clips.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ContentAssets.AUDIO_FOLDER + "/" + clip);
                    definition.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.CreateAsset(sound, path);
                }

                property.objectReferenceValue = sound;
                isChanged = true;
            }

            if (isChanged)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(audio);
            }
        }

        /// <summary>The spell shop on the road near the start, the luck shrine up the northern branch, and two commanders.</summary>
        private static void SeedMission()
        {
            MissionSettingsScriptableObject mission = ContentAssets.Mission;
            bool isChanged = false;
            if (!mission.MapObjects.Any(entry => entry.Kind == MapObjectKind.SpellShop))
            {
                mission.AddMapObject(new MapObjectEntry("hall-of-hymns", "HALL OF FORBIDDEN HYMNS", MapObjectKind.SpellShop, new Vector2Int(26, 30),
                    "Old songs of power are sold here, and the books to keep them in.", LoadSprite(OBJECT_SPRITES + "/SpellShop.png"), 500, 0,
                    new[] { "magic-arrow", "cure", "haste", "bloodlust", "lightning-bolt", "fireball", "meteor-shower", "implosion" }));
                isChanged = true;
            }

            if (!mission.MapObjects.Any(entry => entry.Kind == MapObjectKind.LuckShrine))
            {
                mission.AddMapObject(new MapObjectEntry("fortunes-anvil", "FORTUNE'S ANVIL", MapObjectKind.LuckShrine, new Vector2Int(42, 25),
                    "Strike the anvil once, and fortune rides with you until your next victory.", LoadSprite(OBJECT_SPRITES + "/LuckShrine.png"), 0, 1,
                    new string[0]));
                isChanged = true;
            }

            if (!mission.Encounters.Any(entry => entry.HasCommander))
            {
                foreach (EncounterEntry encounter in mission.Encounters)
                {
                    if (encounter.Kind == EncounterKind.Town)
                    {
                        encounter.SetCommander(mission.ClanHeroName, 1, 15, new[] { "bloodlust", "magic-arrow" });
                    }
                    else if (encounter.Kind == EncounterKind.Boss)
                    {
                        encounter.SetCommander("Morthal of the Black Banner", 2, 30, new[] { "slow", "magic-arrow", "curse" });
                    }
                }

                isChanged = true;
            }

            if (isChanged)
            {
                EditorUtility.SetDirty(mission);
            }
        }
        #endregion

        #region WIDGETS
        /// <summary>A row with an icon, a title, a line of text and an amount on the right; the whole row is a button.</summary>
        private static GameObject CreateIconRow(TypographySettings typography)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ICON_ROW_PATH);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = NewUi("IconRow", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(480f, 72f);
            Image plate = NewImage(root.transform, "Plate", null, false);
            plate.color = CardColor;
            Stretch(plate.rectTransform);
            Image icon = NewImage(root.transform, "Icon", null, false);
            icon.preserveAspect = true;
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(8f, 0f);
            iconRect.sizeDelta = new Vector2(60f, 60f);
            TMP_Text title = Text(root.transform, "Title", typography, TextStyle.Label, 19f, TextAlignmentOptions.BottomLeft, GoldColor);
            Offsets(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(80f, 0f), new Vector2(-150f, -4f));
            TMP_Text description = Text(root.transform, "Description", typography, TextStyle.Body, 16f, TextAlignmentOptions.TopLeft, TextColor);
            Offsets(description.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(80f, 2f), new Vector2(-150f, 0f));
            TMP_Text amount = Text(root.transform, "Amount", typography, TextStyle.Number, 19f, TextAlignmentOptions.MidlineRight, TextColor);
            Offsets(amount.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-145f, 0f), new Vector2(-10f, 0f));
            GameObject glowObject = NewUi("Glow", root.transform);
            Stretch((RectTransform)glowObject.transform);
            Image glow = Glow(glowObject);
            Button button = root.AddComponent<Button>();
            Hover(button, glow);
            UiWidget widget = root.AddComponent<UiWidget>();
            Wire(widget, ("button", button), ("titleText", title), ("descriptionText", description), ("amountText", amount), ("iconImage", icon),
                ("backgroundImage", plate));
            return Save(root, ICON_ROW_PATH);
        }

        /// <summary>A spell's place in the spell book: its icon, name and cost in ink, the whole place a button.</summary>
        private static GameObject CreateSpellSlot(TypographySettings typography)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(SPELL_SLOT_PATH);
            if (existing != null)
            {
                return existing;
            }

            GameObject root = NewUi("SpellSlot", null);
            ((RectTransform)root.transform).sizeDelta = new Vector2(250f, 170f);
            Image icon = NewImage(root.transform, "Icon", null, false);
            icon.preserveAspect = true;
            Anchor(icon.rectTransform, new Vector2(0.25f, 0.36f), new Vector2(0.75f, 0.98f));
            TMP_Text title = Text(root.transform, "Name", typography, TextStyle.Label, 19f, TextAlignmentOptions.Center, InkColor);
            Anchor(title.rectTransform, new Vector2(0f, 0.17f), new Vector2(1f, 0.36f));
            TMP_Text cost = Text(root.transform, "Cost", typography, TextStyle.Body, 15f, TextAlignmentOptions.Center, InkColor);
            Anchor(cost.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.17f));
            GameObject glowObject = NewUi("Glow", root.transform);
            Stretch((RectTransform)glowObject.transform);
            Image glow = Glow(glowObject);
            Button button = root.AddComponent<Button>();
            Hover(button, glow);
            UiWidget widget = root.AddComponent<UiWidget>();
            Wire(widget, ("button", button), ("titleText", title), ("descriptionText", cost), ("iconImage", icon));
            return Save(root, SPELL_SLOT_PATH);
        }
        #endregion

        #region SCREENS
        private static void BuildLevelUp(TypographySettings typography)
        {
            GameObject root = ScreenRoot("LevelUp");
            LevelUpCanvasView view = root.AddComponent<LevelUpCanvasView>();
            Transform frame = Frame(root.transform, Sprite(SPRITES + "/Gameplay/Dialog/PanelFrame.png"), new Vector2(1000f, 600f));
            TMP_Text title = PlacedText(frame, "TitleText", typography, TextStyle.Title, 34f, TextAlignmentOptions.Center, GoldColor, 70, 55, 930, 112);
            TMP_Text subtitle = PlacedText(frame, "SubtitleText", typography, TextStyle.Heading, 22f, TextAlignmentOptions.Center, GoldColor, 70, 114, 930, 152);
            TMP_Text body = PlacedText(frame, "BodyText", typography, TextStyle.Body, 20f, TextAlignmentOptions.Center, TextColor, 70, 154, 930, 190);
            List<Object> cards = new List<Object>
            {
                SkillCard(frame, "LeftOption", typography, new[] { 80f, 200f, 490f, 520f }),
                SkillCard(frame, "RightOption", typography, new[] { 510f, 200f, 920f, 520f })
            };
            UiWidget continueButton = Nest<UiWidget>(AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab"), frame, "ContinueButton");
            Place(continueButton.Rect, new[] { 400f, 440f, 600f, 500f });
            Assign(view, "titleText", title);
            Assign(view, "subtitleText", subtitle);
            Assign(view, "bodyText", body);
            AssignList(view, "optionCards", cards);
            Assign(view, "continueButton", continueButton);
            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.LEVEL_UP_PATH);
            Object.DestroyImmediate(root);
        }

        // a large card for a level-up choice: the skill's icon over its name, what it does and whether it is new
        private static UiWidget SkillCard(Transform parent, string name, TypographySettings typography, float[] box)
        {
            GameObject card = NewUi(name, parent);
            RectTransform rect = (RectTransform)card.transform;
            Place(rect, box);
            Image plate = NewImage(card.transform, "Plate", null, false);
            plate.color = CardColor;
            Stretch(plate.rectTransform);
            Image icon = NewImage(card.transform, "Icon", null, false);
            icon.preserveAspect = true;
            Anchor(icon.rectTransform, new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.96f));
            TMP_Text amount = Text(card.transform, "Kind", typography, TextStyle.Label, 15f, TextAlignmentOptions.Center, TextColor);
            Anchor(amount.rectTransform, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.6f));
            TMP_Text title = Text(card.transform, "Name", typography, TextStyle.Title, 24f, TextAlignmentOptions.Center, GoldColor);
            Anchor(title.rectTransform, new Vector2(0.05f, 0.4f), new Vector2(0.95f, 0.52f));
            TMP_Text description = Text(card.transform, "Description", typography, TextStyle.Body, 18f, TextAlignmentOptions.Top, TextColor);
            Anchor(description.rectTransform, new Vector2(0.07f, 0.04f), new Vector2(0.93f, 0.4f));
            GameObject glowObject = NewUi("Glow", card.transform);
            Stretch((RectTransform)glowObject.transform);
            Image glow = Glow(glowObject);
            Button button = card.AddComponent<Button>();
            Hover(button, glow);
            UiWidget widget = card.AddComponent<UiWidget>();
            Wire(widget, ("button", button), ("titleText", title), ("descriptionText", description), ("amountText", amount), ("iconImage", icon),
                ("backgroundImage", plate));
            return widget;
        }

        private static void BuildSpellBook(TypographySettings typography, GameObject spellSlot)
        {
            GameObject root = ScreenRoot("SpellBook");
            SpellBookCanvasView view = root.AddComponent<SpellBookCanvasView>();
            Transform book = Frame(root.transform, Sprite(SPRITES + "/SpellBook/SpellBook.png"), new Vector2(1400f, 860f), false);
            TMP_Text title = PlacedText(book, "TitleText", typography, TextStyle.Title, 32f, TextAlignmentOptions.Center, InkColor, 90, 60, 690, 108);
            TMP_Text mana = PlacedText(book, "ManaText", typography, TextStyle.Heading, 24f, TextAlignmentOptions.Center, InkColor, 710, 60, 1310, 108);
            float[] columns = { 110f, 420f, 720f, 1030f };
            float[] rows = { 118f, 298f, 478f };
            List<Object> slots = new List<Object>();
            for (int page = 0; page < 2; page++)
            {
                for (int row = 0; row < rows.Length; row++)
                {
                    for (int column = 0; column < 2; column++)
                    {
                        float x = columns[page * 2 + column];
                        UiWidget slot = Nest<UiWidget>(spellSlot, book, "Spell" + (slots.Count + 1));
                        Place(slot.Rect, new[] { x, rows[row], x + 260f, rows[row] + 170f });
                        slots.Add(slot);
                    }
                }
            }

            TMP_Text description = PlacedText(book, "DescriptionText", typography, TextStyle.Body, 19f, TextAlignmentOptions.Center, InkColor, 740, 652, 1300, 752);
            GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab");
            UiWidget previous = Nest<UiWidget>(buttonPrefab, book, "PreviousButton");
            Place(previous.Rect, new[] { 70f, 756f, 190f, 812f });
            UiWidget next = Nest<UiWidget>(buttonPrefab, book, "NextButton");
            Place(next.Rect, new[] { 1210f, 756f, 1330f, 812f });
            TMP_Text pageText = PlacedText(book, "PageText", typography, TextStyle.Label, 20f, TextAlignmentOptions.Center, InkColor, 210, 762, 420, 806);
            UiWidget close = Nest<UiWidget>(buttonPrefab, book, "CloseButton");
            Place(close.Rect, new[] { 600f, 756f, 800f, 812f });
            Assign(view, "titleText", title);
            Assign(view, "manaText", mana);
            Assign(view, "descriptionText", description);
            Assign(view, "pageText", pageText);
            AssignList(view, "spellSlots", slots);
            Assign(view, "previousButton", previous);
            Assign(view, "nextButton", next);
            Assign(view, "closeButton", close);
            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.SPELL_BOOK_PATH);
            Object.DestroyImmediate(root);
        }

        private static void BuildSpellShop(TypographySettings typography, GameObject iconRow)
        {
            GameObject root = ScreenRoot("SpellShop");
            SpellShopCanvasView view = root.AddComponent<SpellShopCanvasView>();
            Transform frame = Frame(root.transform, Sprite(SPRITES + "/Gameplay/Dialog/PanelFrame.png"), new Vector2(1100f, 920f));
            TMP_Text title = PlacedText(frame, "TitleText", typography, TextStyle.Title, 34f, TextAlignmentOptions.Center, GoldColor, 70, 55, 1030, 110);
            TMP_Text subtitle = PlacedText(frame, "SubtitleText", typography, TextStyle.Label, 19f, TextAlignmentOptions.Center, TextColor, 70, 114, 1030, 150);
            UiWidget bookRow = Nest<UiWidget>(iconRow, frame, "SpellBookRow");
            Place(bookRow.Rect, new[] { 80f, 160f, 1020f, 232f });
            List<Object> rows = new List<Object>();
            for (int i = 0; i < 8; i++)
            {
                UiWidget row = Nest<UiWidget>(iconRow, frame, "Spell" + (i + 1));
                float top = 246f + i * 68f;
                Place(row.Rect, new[] { 80f, top, 1020f, top + 64f });
                rows.Add(row);
            }

            UiWidget close = Nest<UiWidget>(AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab"), frame, "CloseButton");
            Place(close.Rect, new[] { 450f, 800f, 650f, 856f });
            Assign(view, "titleText", title);
            Assign(view, "subtitleText", subtitle);
            Assign(view, "bookRow", bookRow);
            AssignList(view, "spellRows", rows);
            Assign(view, "closeButton", close);
            Assign(view, "bookIcon", LoadSprite(SPRITES + "/SpellBook/SpellBookIcon.png"));
            PrefabUtility.SaveAsPrefabAsset(root, ContentAssets.SPELL_SHOP_PATH);
            Object.DestroyImmediate(root);
        }

        /// <summary>
        /// The hero screen gets the magic stats beside the others, tabs for the inventory and the skills over the inventory's
        /// heading, the spell book button and the skills panel over the inventory grid.
        /// </summary>
        private static void ExtendHeroScreen(TypographySettings typography, GameObject iconRow)
        {
            string path = ContentAssets.UI_FOLDER + "/Screens/Hero.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                HeroCanvasView view = root.GetComponent<HeroCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("magicText").objectReferenceValue != null)
                {
                    return;
                }

                TMP_Text stats = (TMP_Text)serialized.FindProperty("statsText").objectReferenceValue;
                Place(stats.rectTransform, new[] { 172f, 796f, 344f, 960f });
                TMP_Text magic = Object.Instantiate(stats.gameObject, stats.transform.parent).GetComponent<TMP_Text>();
                magic.name = "MagicText";
                Place(magic.rectTransform, new[] { 350f, 796f, 572f, 960f });
                Transform inventoryTitle = root.transform.Find("InventoryTitle");
                if (inventoryTitle != null)
                {
                    inventoryTitle.gameObject.SetActive(false);
                }

                TMP_Text hint = (TMP_Text)serialized.FindProperty("hintText").objectReferenceValue;
                Place(hint.rectTransform, new[] { 1366f, 778f, 1856f, 800f });
                hint.alignment = TextAlignmentOptions.Center;
                GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab");
                UiWidget inventoryTab = Nest<UiWidget>(buttonPrefab, root.transform, "InventoryTab");
                Place(inventoryTab.Rect, new[] { 1360f, 90f, 1530f, 140f });
                UiWidget skillsTab = Nest<UiWidget>(buttonPrefab, root.transform, "SkillsTab");
                Place(skillsTab.Rect, new[] { 1536f, 90f, 1706f, 140f });
                UiWidget spellBook = Nest<UiWidget>(buttonPrefab, root.transform, "SpellBookButton");
                Place(spellBook.Rect, new[] { 1712f, 90f, 1882f, 140f });

                Image panel = NewImage(root.transform, "SkillsPanel", null, true);
                panel.color = PanelColor;
                Place(panel.rectTransform, new[] { 1352f, 144f, 1866f, 776f });
                List<Object> rows = new List<Object>();
                for (int i = 0; i < 8; i++)
                {
                    UiWidget row = Nest<UiWidget>(iconRow, panel.transform, "Skill" + (i + 1));
                    float top = 8f + i * 77f;
                    Place(row.Rect, new[] { 10f, top, 504f, top + 72f });
                    rows.Add(row);
                }

                panel.gameObject.SetActive(false);
                serialized.FindProperty("magicText").objectReferenceValue = magic;
                serialized.FindProperty("inventoryTab").objectReferenceValue = inventoryTab;
                serialized.FindProperty("skillsTab").objectReferenceValue = skillsTab;
                serialized.FindProperty("spellBookButton").objectReferenceValue = spellBook;
                serialized.FindProperty("skillsPanel").objectReferenceValue = panel.gameObject;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssignList(view, "skillRows", rows);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The battle screen's chronicle heading makes room for the spell book and surrender buttons.</summary>
        private static void ExtendCombatScreen()
        {
            string path = ContentAssets.UI_FOLDER + "/Screens/Combat.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CombatCanvasView view = root.GetComponent<CombatCanvasView>();
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("spellBookButton").objectReferenceValue != null)
                {
                    return;
                }

                Transform logTitle = root.transform.Find("LogTitle");
                if (logTitle != null)
                {
                    logTitle.gameObject.SetActive(false);
                }

                GameObject buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WIDGETS + "/Button.prefab");
                UiWidget spellBook = Nest<UiWidget>(buttonPrefab, root.transform, "SpellBookButton");
                Place(spellBook.Rect, new[] { 1366f, 574f, 1612f, 632f });
                UiWidget surrender = Nest<UiWidget>(buttonPrefab, root.transform, "SurrenderButton");
                Place(surrender.Rect, new[] { 1620f, 574f, 1866f, 632f });
                serialized.FindProperty("spellBookButton").objectReferenceValue = spellBook;
                serialized.FindProperty("surrenderButton").objectReferenceValue = surrender;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The new modal screens sit beside the encounter dialog in GameUiRoot, wired to GameHud.</summary>
        private static void NestIntoRoot()
        {
            string rootPath = ContentAssets.UI_FOLDER + "/GameUiRoot.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(rootPath);
            try
            {
                GameHud hud = root.GetComponent<GameHud>();
                SerializedObject serialized = new SerializedObject(hud);
                bool isChanged = false;
                Transform dialog = ((Component)serialized.FindProperty("dialogView").objectReferenceValue).transform;
                int index = dialog.GetSiblingIndex();
                foreach ((string field, string path) in new[]
                {
                    ("levelUpView", ContentAssets.LEVEL_UP_PATH), ("spellBookView", ContentAssets.SPELL_BOOK_PATH), ("spellShopView", ContentAssets.SPELL_SHOP_PATH)
                })
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    if (property.objectReferenceValue != null)
                    {
                        continue;
                    }

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), dialog.parent);
                    instance.transform.SetSiblingIndex(++index);
                    instance.SetActive(false);
                    property.objectReferenceValue = instance.GetComponent<CanvasScreenView>();
                    isChanged = true;
                }

                if (isChanged)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, rootPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A screen root like the encounter dialog's: the full design frame with its own Canvas and a CanvasGroup.</summary>
        private static GameObject ScreenRoot(string name)
        {
            GameObject root = NewUi(name, null);
            RectTransform rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(1920f, 1080f);
            root.AddComponent<Canvas>();
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<CanvasGroup>();
            Image blocker = NewImage(root.transform, "InputBlocker", null, true);
            blocker.color = DimColor;
            Stretch(blocker.rectTransform);
            return root;
        }

        private static Transform Frame(Transform root, Sprite sprite, Vector2 size, bool isSliced = true)
        {
            Image frame = NewImage(root, "Frame", sprite, true);
            if (isSliced)
            {
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = 2f;
            }

            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return frame.transform;
        }

        private static TMP_Text PlacedText(Transform parent, string name, TypographySettings typography, TextStyle style, float size, TextAlignmentOptions alignment,
            Color color, float x0, float y0, float x1, float y1)
        {
            TMP_Text text = Text(parent, name, typography, style, size, alignment, color);
            Place(text.rectTransform, new[] { x0, y0, x1, y1 });
            return text;
        }
        #endregion

        #region HELPERS
        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite Sprite(string path)
        {
            Sprite sprite = LoadSprite(path);
            if (sprite == null)
            {
                throw new InvalidOperationException("Missing sprite " + path + "; run Tools/UiArt/build_magic.py first.");
            }

            return sprite;
        }

        private static GameObject NewUi(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            gameObject.layer = LayerMask.NameToLayer("UI");
            return gameObject;
        }

        private static Image NewImage(Transform parent, string name, Sprite sprite, bool isRaycastTarget)
        {
            Image image = NewUi(name, parent).AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = isRaycastTarget;
            return image;
        }

        private static Image Glow(GameObject target)
        {
            Image glow = target.AddComponent<Image>();
            glow.sprite = LoadSprite(SPRITES + "/Gameplay/Common/ButtonGlow.png");
            glow.type = Image.Type.Sliced;
            glow.color = Color.white;
            glow.raycastTarget = true;
            return glow;
        }

        /// <summary>The glow is invisible at rest and shows on hover, press and keyboard selection; a disabled button is dimmed.</summary>
        private static void Hover(Button button, Graphic glow)
        {
            button.targetGraphic = glow;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 0f);
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.55f);
            colors.pressedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.selectedColor = new Color(1f, 1f, 1f, 0.3f);
            colors.disabledColor = new Color(0.08f, 0.06f, 0.05f, 0.55f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.12f;
            button.colors = colors;
        }

        private static TMP_Text Text(Transform parent, string name, TypographySettings typography, TextStyle style, float size, TextAlignmentOptions alignment,
            Color color)
        {
            GameObject gameObject = NewUi(name, parent);
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = alignment;
            text.color = color;
            text.fontSize = size;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * AUTO_SIZE_MINIMUM;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            gameObject.AddComponent<TypographyText>().Configure(typography, style);
            return text;
        }

        private static T Nest<T>(GameObject prefab, Transform parent, string name)
            where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance.GetComponent<T>();
        }

        private static GameObject Save(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Places a rect given in the parent's units from its top left: x0, y0, x1, y1.</summary>
        private static void Place(RectTransform rect, float[] box)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(box[0], -box[1]);
            rect.sizeDelta = new Vector2(box[2] - box[0], box[3] - box[1]);
        }

        private static void Stretch(RectTransform rect)
        {
            Anchor(rect, Vector2.zero, Vector2.one);
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            Offsets(rect, min, max, Vector2.zero, Vector2.zero);
        }

        private static void Offsets(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void Wire(Object target, params (string Field, Object Value)[] values)
        {
            SerializedObject serialized = new SerializedObject(target);
            foreach ((string field, Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(field);
                if (property != null)
                {
                    property.objectReferenceValue = value;
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Assign(Object target, string field, Object value)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no serialized field " + field);
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AssignList(Object target, string field, List<Object> values)
        {
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null || !property.isArray)
            {
                throw new InvalidOperationException(target.GetType().Name + " has no serialized list " + field);
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion
    }
}
