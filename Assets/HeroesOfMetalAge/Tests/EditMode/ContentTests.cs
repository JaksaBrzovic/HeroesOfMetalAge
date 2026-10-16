using System;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Bootstrap;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Infrastructure.Audio;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Presentation;
using MLin.Audio;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Authored databases are valid, and edits to them reach gameplay, presentation and audio.</summary>
    public class ContentTests
    {
        private UnitDatabaseScriptableObject units;
        private ArtefactDatabaseScriptableObject artefacts;
        private HeroDataBaseScriptableObject heroes;
        private AudioDatabaseScriptableObject audio;
        private MissionSettingsScriptableObject mission;

        [SetUp]
        public void SetUp()
        {
            // copies, so edits never touch the authored assets
            units = Object.Instantiate(ContentAssets.Units);
            artefacts = Object.Instantiate(ContentAssets.Artefacts);
            heroes = Object.Instantiate(ContentAssets.Heroes);
            audio = Object.Instantiate(ContentAssets.Audio);
            mission = Object.Instantiate(ContentAssets.Mission);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object copy in new Object[] { units, artefacts, heroes, audio, mission })
            {
                Object.DestroyImmediate(copy);
            }
        }

        [Test]
        public void DomainAndApplication_HaveNoUnityDependency()
        {
            Assert.IsFalse(typeof(DefinitionCatalog).Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)));
            Assert.IsFalse(typeof(GameSession).Assembly.GetReferencedAssemblies().Any(reference => reference.Name.StartsWith("UnityEngine", StringComparison.Ordinal)));
        }

        [Test]
        public void AuthoredContent_IsValid()
        {
            Assert.DoesNotThrow(() => ContentAssets.LoadCatalog());
        }

        [Test]
        public void EditedUnitStats_ReachCatalogAndBattle()
        {
            UnitEntry unit = units.Units[0];
            unit.Health += 5;
            unit.MinimumDamage += 2;
            unit.MaximumDamage += 2;

            DefinitionCatalog edited = Load();
            DefinitionCatalog original = ContentAssets.LoadCatalog();
            UnitDefinition changed = edited.Unit(unit.Id);
            CampaignState campaign = new CampaignRules(edited).NewCampaign();
            BattleEngine battle = new BattleEngine(edited, new SeededRandomSource(2), campaign, edited.Mission.Encounter(EncounterKind.Sentinel));

            Assert.AreEqual(unit.Health, changed.Health);
            Assert.AreEqual(edited.Unit(campaign.Army[0].UnitId).Health, battle.Combatants[0].HealthPerUnit);
            Assert.Greater(DamageCalculator.Calculate(changed, 10, 2, changed, 2, false, true, 0, edited.Mission.Combat),
                DamageCalculator.Calculate(original.Unit(unit.Id), 10, 2, original.Unit(unit.Id), 2, false, true, 0, edited.Mission.Combat));
        }

        [Test]
        public void EditedArtefactBonuses_ReachHeroStats()
        {
            ArtefactEntry artefact = artefacts.Artefacts[0];
            artefact.AttackBonus += 7;
            artefact.DefenseBonus += 5;
            DefinitionCatalog edited = Load();
            CampaignRules rules = new CampaignRules(edited);
            CampaignState campaign = rules.NewCampaign();
            int attack = rules.Attack(campaign);
            int defense = rules.Defense(campaign);

            rules.DebugArtefacts(campaign);
            rules.Equip(campaign, 0);

            Assert.AreEqual(attack + artefact.AttackBonus, rules.Attack(campaign));
            Assert.AreEqual(defense + artefact.DefenseBonus, rules.Defense(campaign));
        }

        [Test]
        public void EditedHeroTextAndPortrait_ReachPresentation()
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite portrait = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            try
            {
                HeroEntry hero = heroes.Heroes[0];
                hero.DisplayName = "EDITED HERO";
                hero.Description = "Edited description";
                hero.Portrait = portrait;

                DefinitionCatalog edited = Load();
                PresentationAssets media = GameBootstrap.CreatePresentationAssets(units, artefacts, heroes, mission);

                Assert.AreEqual("EDITED HERO", edited.Hero(hero.Id).Name);
                Assert.AreEqual("Edited description", edited.Hero(hero.Id).Description);
                Assert.AreSame(portrait, media.Hero(hero.Id));
            }
            finally
            {
                Object.DestroyImmediate(portrait);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void InvalidAuthoredStats_AreRejected()
        {
            units.Units[0].Health = 0;

            Assert.Throws<InvalidOperationException>(() => Load());
        }

        [Test]
        public void EveryCue_HasASoundAndLayersLoop()
        {
            foreach (SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                SoundDefinition sound = audio.Sound(cue);
                Assert.IsTrue(sound != null && sound.HasClips, "cue without clips " + cue);
            }

            foreach (MusicLayer layer in Enum.GetValues(typeof(MusicLayer)))
            {
                SoundDefinition sound = audio.Layer(layer);
                Assert.IsTrue(sound != null && sound.Loop && sound.OutputGroup != null && sound.OutputGroup.name == GameAudio.MUSIC_CHANNEL, layer + " must loop on Music");
            }

            foreach (SoundDefinition sound in new[] { audio.Music(MusicTrack.Battle), audio.Music(MusicTrack.Victory), audio.Music(MusicTrack.Defeat), mission.TownMusic })
            {
                Assert.IsTrue(sound != null && sound.HasClips && sound.OutputGroup != null && sound.OutputGroup.name == GameAudio.MUSIC_CHANNEL, sound + " on Music");
            }

            // the map music clip is a local file kept out of version control, so only its definition is required here
            Assert.IsTrue(mission.MapMusic != null && mission.MapMusic.OutputGroup != null && mission.MapMusic.OutputGroup.name == GameAudio.MUSIC_CHANNEL,
                "map music on Music");
            Assert.IsTrue(mission.MapMusic.Loop && mission.TownMusic.Loop, "map and town music loop");

            SoundDefinition battle = audio.Music(MusicTrack.Battle);
            Assert.IsTrue(battle.Loop && battle.IntroClip != null, "battle music plays its intro once, then loops");
            Assert.IsTrue(audio.Music(MusicTrack.Victory).Loop, "victory music loops until the rewards screen closes");
            Assert.IsTrue(audio.RidingLoop != null && audio.RidingLoop.Loop && audio.RidingLoop.OutputGroup.name == GameAudio.EFFECTS_CHANNEL,
                "riding loop loops on Effects");
        }

        [Test]
        public void MissingMusicTrack_IsRejected()
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(audio);
            serialized.FindProperty("defeatMusic").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(() => Load());
        }

        [Test]
        public void EveryUnit_HasAVoiceOnTheVoiceChannel()
        {
            foreach (UnitEntry unit in units.Units)
            {
                Assert.IsTrue(unit.Voice != null && unit.Voice.HasClips && unit.Voice.OutputGroup != null, unit.Id + " voice");
                Assert.AreEqual(GameAudio.VOICE_CHANNEL, unit.Voice.OutputGroup.name, unit.Id + " voice channel");
            }
        }

        [Test]
        public void VoiceWithoutClips_IsRejected()
        {
            SoundDefinition silent = ScriptableObject.CreateInstance<SoundDefinition>();
            try
            {
                units.Units[0].Voice = silent;

                Assert.Throws<InvalidOperationException>(() => Load());
            }
            finally
            {
                Object.DestroyImmediate(silent);
            }
        }

        [Test]
        public void AudioSettings_RouteChannelsAndDuckMusicUnderVoice()
        {
            AudioSystemSettings settings = audio.Settings;
            string[] channels = settings.VolumeChannels.Select(channel => channel.Name).ToArray();

            Assert.IsNotNull(settings.Mixer);
            CollectionAssert.AreEquivalent(new[] { GameAudio.MASTER_CHANNEL, GameAudio.MUSIC_CHANNEL, GameAudio.EFFECTS_CHANNEL, GameAudio.VOICE_CHANNEL }, channels);
            Assert.IsTrue(settings.DuckingRules.Any(rule => rule.IsValid && rule.Trigger.name == "Voice" && rule.Target.name == "Music"));
        }

        [Test]
        public void SwappedUnitVoice_IsUsedByTheAudioAdapter()
        {
            UnitEntry unit = units.Units[0];
            SoundDefinition replacement = units.Units[1].Voice;
            unit.Voice = replacement;
            GameObject host = new GameObject("audio adapter test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(Load(), new MemorySaveStore());
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(audio, units, mission, session, new FrontEndFlow(0f, 0f, IntroScript.None, 0f, 0f, false));

                Assert.AreSame(replacement, adapter.VoiceFor(unit.Id));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Strikes_PlaySwingThenHitOrMiss()
        {
            GameObject host = new GameObject("audio adapter test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(Load(), new MemorySaveStore());
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(audio, units, mission, session, new FrontEndFlow(0f, 0f, IntroScript.None, 0f, 0f, false));
                int heavy = audio.HeavyHitThreshold;

                CollectionAssert.AreEqual(new[] { SoundCue.SwordSwing, SoundCue.SwordHit }, adapter.StrikeCues(Strike(1, false, false)));
                CollectionAssert.AreEqual(new[] { SoundCue.SwordSwing, SoundCue.SwordMiss }, adapter.StrikeCues(Strike(0, false, true)));
                CollectionAssert.AreEqual(new[] { SoundCue.ArrowFire, SoundCue.ArrowHit, SoundCue.HeavyHit }, adapter.StrikeCues(Strike(heavy, true, false)));
                CollectionAssert.AreEqual(new[] { SoundCue.ArrowFire, SoundCue.ArrowMiss }, adapter.StrikeCues(Strike(0, true, true)));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MusicFollowsTheMapTownBattleAndDefeat()
        {
            GameObject host = new GameObject("audio adapter test");
            try
            {
                GameSession session = CampaignTestUtility.NewSession(Load(), new MemorySaveStore());
                GameAudio adapter = host.AddComponent<GameAudio>();
                adapter.Initialize(audio, units, mission, session, new FrontEndFlow(0f, 0f, IntroScript.None, 0f, 0f, false));
                MusicTrack? map = mission.MapMusic.HasClips ? MusicTrack.Map : (MusicTrack?)null;
                Assert.AreEqual(map, adapter.CurrentTrack, "the adventure map starts with the map music (the layers when its local clip is missing)");

                session.StartMission();
                session.DebugTown();
                Assert.AreEqual(MusicTrack.Town, adapter.CurrentTrack, "the town screen plays the town music");

                session.LeaveTown();
                Assert.AreEqual(map, adapter.CurrentTrack, "leaving the town returns to the map music");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static DamageEvent Strike(int damage, bool isRanged, bool isMiss)
        {
            return new DamageEvent(0, 1, "a", "b", damage, 0, false, false, isRanged, isMiss);
        }

        private DefinitionCatalog Load()
        {
            return ContentLoader.Load(units, artefacts, heroes, audio, mission);
        }
    }
}
