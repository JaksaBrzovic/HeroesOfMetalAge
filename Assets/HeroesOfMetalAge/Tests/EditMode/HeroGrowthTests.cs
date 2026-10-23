using System.Collections.Generic;
using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Experience, level-ups, secondary skills and what they do outside battle, and the saves that keep them.</summary>
    public class HeroGrowthTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private LevelUpRules levelUps;
        private HeroPolicy policy;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            levelUps = new LevelUpRules(catalog, rules);
            policy = catalog.HeroPolicy;
        }

        [Test]
        public void ExperienceTable_FollowsTheAuthoredThresholdsThenGrows()
        {
            ExperienceTable table = new ExperienceTable(new[] { 100, 250 }, 2d, 6);

            Assert.AreEqual(0, table.Threshold(1));
            Assert.AreEqual(100, table.Threshold(2));
            Assert.AreEqual(250, table.Threshold(3));
            Assert.AreEqual(550, table.Threshold(4), "the last step of 150 doubles to 300");
            Assert.AreEqual(1150, table.Threshold(5));
            Assert.AreEqual(1, table.Level(99));
            Assert.AreEqual(3, table.Level(549));
            Assert.AreEqual(6, table.Level(1000000), "capped at the maximum level");
        }

        [Test]
        public void NewCampaign_StartsWithTheHerosSkillsAndFullMana()
        {
            CampaignState state = rules.NewCampaign();
            HeroDefinition hero = catalog.Hero(state.HeroId);

            CollectionAssert.AreEqual(hero.StartingSkills.Select(skill => skill.Skill), state.Skills.Select(skill => skill.Skill));
            Assert.AreEqual(rules.MaximumMana(state), state.Mana);
            Assert.AreEqual(1, state.LevelsGranted);
            Assert.IsNull(state.PendingLevelUp);
        }

        [Test]
        public void LevelUp_RaisesOnePrimaryStatAndOffersTwoSkills()
        {
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(2));
            int before = Enumerable.Range(0, CampaignState.PRIMARY_STAT_COUNT).Sum(stat => rules.Primary(state, (PrimaryStat)stat));

            Assert.IsTrue(levelUps.Advance(state, new SeededRandomSource(3)));

            LevelUpOffer offer = state.PendingLevelUp;
            Assert.AreEqual(2, offer.Level);
            Assert.AreEqual(before + 1, Enumerable.Range(0, CampaignState.PRIMARY_STAT_COUNT).Sum(stat => rules.Primary(state, (PrimaryStat)stat)));
            Assert.AreEqual(LevelUpRules.OPTION_COUNT, offer.Options.Count);
            Assert.AreNotEqual(offer.Options[0].Skill, offer.Options[1].Skill);
            Assert.IsTrue(offer.Options.All(option => (int)option.Level == (int)state.SkillLevelOf(option.Skill) + 1));
            Assert.IsFalse(levelUps.Advance(state, new SeededRandomSource(3)), "the next level waits for the choice");
        }

        [Test]
        public void LevelUp_FirstOptionImprovesAKnownSkillAndSecondIsNew()
        {
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(2));
            levelUps.Advance(state, new SeededRandomSource(5));

            Assert.AreNotEqual(SkillLevel.None, state.SkillLevelOf(state.PendingLevelUp.Options[0].Skill), "an upgrade of a known skill");
            Assert.AreEqual(SkillLevel.None, state.SkillLevelOf(state.PendingLevelUp.Options[1].Skill), "a new skill");
        }

        [Test]
        public void Choose_LearnsTheSkill_ThenTheNextLevelIsGranted()
        {
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(3));
            IRandomSource random = new SeededRandomSource(9);
            levelUps.Advance(state, random);
            HeroSkill chosen = state.PendingLevelUp.Options[1];

            Assert.IsTrue(levelUps.Choose(state, 1).IsSuccess);
            Assert.AreEqual(chosen.Level, state.SkillLevelOf(chosen.Skill));
            Assert.IsNull(state.PendingLevelUp);
            Assert.IsTrue(levelUps.Advance(state, random));
            Assert.AreEqual(3, state.PendingLevelUp.Level);
            Assert.IsFalse(levelUps.Choose(state, 5).IsSuccess, "an option that is not offered");
        }

        [Test]
        public void LevelUp_OnlyOffersSkillsTheClassCanLearn()
        {
            HeroClassDefinition warrior = catalog.ClassOf(rules.NewCampaign().HeroId);
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(30));
            IRandomSource random = new SeededRandomSource(11);
            while (levelUps.Advance(state, random))
            {
                Assert.IsTrue(state.PendingLevelUp.Options.All(option => warrior.SkillWeight(option.Skill) > 0 || state.SkillLevelOf(option.Skill) != SkillLevel.None));
                levelUps.Choose(state, 1);
            }

            Assert.LessOrEqual(state.Skills.Count, policy.MaximumSkills);
        }

        [Test]
        public void LevelUp_OffersWisdomWithinTheClassInterval()
        {
            HeroClassDefinition warrior = catalog.ClassOf(rules.NewCampaign().HeroId);
            Assume.That(warrior.WisdomInterval, Is.GreaterThan(0));
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(warrior.WisdomInterval + 1));
            // always the lowest roll, so the weighted picks keep choosing the first candidates and Wisdom only comes when due
            IRandomSource random = new FixedRandomSource(0);
            bool isOffered = false;
            while (levelUps.Advance(state, random))
            {
                isOffered |= state.PendingLevelUp.Options.Any(option => option.Skill == SecondarySkill.Wisdom);
                levelUps.Choose(state, 0);
            }

            Assert.IsTrue(isOffered, "Wisdom is offered at the latest every " + warrior.WisdomInterval + " levels");
        }

        [Test]
        public void LevelUp_WithEverySkillMastered_OffersNothing()
        {
            CampaignSnapshot snapshot = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(2)).Snapshot();
            snapshot.skills = policy.Skills.Take(policy.MaximumSkills)
                .Select(skill => new SkillSnapshot { skill = skill.Skill.ToString(), level = (int)SkillLevel.Expert })
                .ToList();
            CampaignState state = rules.Restore(snapshot);

            Assert.IsTrue(levelUps.Advance(state, new SeededRandomSource(1)));
            Assert.AreEqual(0, state.PendingLevelUp.Options.Count);
            Assert.IsTrue(levelUps.Choose(state, 0).IsSuccess);
        }

        [Test]
        public void Skills_ChangeMovementGoldManaAndMorale()
        {
            CampaignState plain = rules.NewCampaign();
            CampaignState skilled = WithSkills(plain, (SecondarySkill.Logistics, SkillLevel.Basic), (SecondarySkill.Estates, SkillLevel.Advanced),
                (SecondarySkill.Intelligence, SkillLevel.Expert), (SecondarySkill.Leadership, SkillLevel.Expert), (SecondarySkill.Luck, SkillLevel.Basic),
                (SecondarySkill.Mysticism, SkillLevel.Basic));
            HeroDefinition hero = catalog.Hero(plain.HeroId);

            Assert.AreEqual(Mathf.RoundToInt(hero.DailyMovement * (1f + (float)policy.Value(SecondarySkill.Logistics, SkillLevel.Basic))), rules.DailyMovement(skilled));
            Assert.AreEqual((int)policy.Value(SecondarySkill.Estates, SkillLevel.Advanced), rules.DailyIncome(skilled));
            Assert.AreEqual((int)(rules.Knowledge(skilled) * policy.ManaPerKnowledge * (1 + policy.Value(SecondarySkill.Intelligence, SkillLevel.Expert))),
                rules.MaximumMana(skilled));
            Assert.AreEqual(CampaignRules.MAXIMUM_MORALE, rules.Morale(skilled), "Expert Leadership: +3");
            Assert.AreEqual(1, rules.Luck(skilled));
            Assert.AreEqual(policy.ManaRegeneration + (int)policy.Value(SecondarySkill.Mysticism, SkillLevel.Basic), rules.ManaRegeneration(skilled));
        }

        [Test]
        public void EndDay_PaysEstatesAndRegrowsManaUpToTheMaximum()
        {
            CampaignSnapshot snapshot = WithSkills(rules.NewCampaign(), (SecondarySkill.Estates, SkillLevel.Basic), (SecondarySkill.Mysticism, SkillLevel.Expert)).Snapshot();
            snapshot.mana = 0;
            CampaignState state = rules.Restore(snapshot);
            int gold = state.Gold;

            Assert.IsTrue(rules.EndDay(state, out _).IsSuccess);

            Assert.AreEqual(gold + rules.DailyIncome(state), state.Gold);
            Assert.AreEqual(Mathf.Min(rules.MaximumMana(state), rules.ManaRegeneration(state)), state.Mana);
            for (int day = 0; day < 20; day++)
            {
                rules.EndDay(state, out _);
            }

            Assert.AreEqual(rules.MaximumMana(state), state.Mana, "never past the maximum");
        }

        [Test]
        public void Pathfinding_CutsOnlyTheExtraCostOfRoughTerrain()
        {
            TerrainRules terrain = catalog.Mission.TerrainRules;
            int swamp = terrain.Cost(TerrainType.Swamp);
            int road = terrain.Cost(TerrainType.Road);

            Assert.AreEqual(100 + (swamp - 100) / 2, terrain.StepCost(TerrainType.Swamp, false, 50, 100));
            Assert.AreEqual(road, terrain.StepCost(TerrainType.Road, false, 75, 100), "roads are cheaper than plain ground already");
            CampaignState skilled = WithSkills(rules.NewCampaign(), (SecondarySkill.Pathfinding, SkillLevel.Advanced));
            Assert.AreEqual(Mathf.RoundToInt((float)policy.Value(SecondarySkill.Pathfinding, SkillLevel.Advanced) * 100f), rules.Pathfinding(skilled).ReductionPercent);
        }

        [Test]
        public void Snapshot_RoundTripsSkillsLevelUpsSpellsAndMana()
        {
            CampaignState state = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(3));
            levelUps.Advance(state, new SeededRandomSource(2));
            levelUps.Choose(state, 0);
            levelUps.Advance(state, new SeededRandomSource(2));
            rules.DebugSpells(state);
            CampaignSnapshot snapshot = state.Snapshot();
            snapshot.luck = 2;

            CampaignState restored = rules.Restore(snapshot);

            Assert.AreEqual(JsonUtility.ToJson(snapshot), JsonUtility.ToJson(restored.Snapshot()));
            Assert.AreEqual(3, restored.PendingLevelUp.Level);
            Assert.IsTrue(restored.HasSpellBook && catalog.Spells.All(spell => restored.KnowsSpell(spell.Id)));
        }

        [Test]
        public void Restore_RejectsInconsistentGrowth()
        {
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.primaryGains[0] = 3;
            Assert.Throws<System.ArgumentException>(() => rules.Restore(snapshot), "stats won without levels");

            snapshot = rules.NewCampaign().Snapshot();
            snapshot.skills.Add(new SkillSnapshot { skill = snapshot.skills[0].skill, level = 1 });
            Assert.Throws<System.ArgumentException>(() => rules.Restore(snapshot), "a skill known twice");

            snapshot = rules.NewCampaign().Snapshot();
            snapshot.spells.Clear();
            snapshot.spells.Add(catalog.Spells[0].Id);
            snapshot.spellBook = false;
            Assert.Throws<System.ArgumentException>(() => rules.Restore(snapshot), "spells without a book");
        }

        [Test]
        public void Restore_MigratesAVersion4Save()
        {
            CampaignSnapshot snapshot = WithExperience(rules.NewCampaign(), policy.Experience.Threshold(4)).Snapshot();
            snapshot.version = CampaignSnapshot.OLDEST_MIGRATED_VERSION;
            snapshot.skills = null;
            snapshot.primaryGains = null;
            snapshot.spells = null;
            snapshot.levelsGranted = 0;
            snapshot.mana = 0;

            CampaignState state = rules.Restore(snapshot);

            Assert.AreEqual(1, state.LevelsGranted);
            Assert.AreEqual(rules.MaximumMana(state), state.Mana);
            Assert.AreEqual(catalog.Hero(state.HeroId).StartingSkills.Count, state.Skills.Count);
            Assert.IsTrue(levelUps.IsDue(state), "the levels reached before skills existed are still to be granted");
        }

        [Test]
        public void SpellShop_SellsTheBookAndSpellsUpToTheHerosWisdom()
        {
            MapObjectDefinition shop = catalog.Mission.Objects.First(mapObject => mapObject.Kind == MapObjectKind.SpellShop);
            MapObjectRules objects = new MapObjectRules(catalog, rules);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.x = shop.Position.X;
            snapshot.y = shop.Position.Y;
            snapshot.gold = 100000;
            // a hero without the book, whatever the hero database gives at the start
            snapshot.spellBook = false;
            snapshot.spells.Clear();
            CampaignState state = rules.Restore(snapshot);
            string firstSpell = shop.SpellIds[0];
            string highSpell = shop.SpellIds.First(id => catalog.Spell(id).Level > policy.SpellLevelWithoutWisdom);

            Assert.IsFalse(objects.BuySpell(state, shop, firstSpell).IsSuccess, "no spell book yet");
            Assert.IsTrue(objects.BuySpellBook(state, shop).IsSuccess);
            Assert.AreEqual(100000 - shop.Gold, state.Gold);
            Assert.IsTrue(objects.BuySpell(state, shop, firstSpell).IsSuccess);
            Assert.IsFalse(objects.BuySpell(state, shop, firstSpell).IsSuccess, "already known");
            Assert.IsFalse(objects.BuySpell(state, shop, highSpell).IsSuccess, "above the hero's Wisdom");

            CampaignState wise = WithSkills(state, (SecondarySkill.Wisdom, SkillLevel.Expert));
            Assert.IsTrue(objects.BuySpell(wise, shop, highSpell).IsSuccess);
        }

        [Test]
        public void LuckShrine_RaisesLuckUntilTheNextVictory()
        {
            MapObjectDefinition shrine = catalog.Mission.Objects.First(mapObject => mapObject.Kind == MapObjectKind.LuckShrine);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.x = shrine.Position.X;
            snapshot.y = shrine.Position.Y;
            CampaignState state = rules.Restore(snapshot);
            int luck = rules.Luck(state);

            Assert.IsTrue(new MapObjectRules(catalog, rules).Visit(state, shrine, out VisitOutcome outcome).IsSuccess);
            Assert.AreEqual(luck + shrine.LuckBonus, rules.Luck(state));
            Assert.AreEqual(shrine.LuckBonus, outcome.LuckBonus);

            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            rules.ApplyVictory(state, sentinel, state.Army, new VictoryRewards(1, new string[0], false));
            Assert.AreEqual(luck, rules.Luck(state));
        }

        [Test]
        public void Diplomacy_LetsAMuchWeakerArmyJoinForGold()
        {
            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.army[0].count = 50000;
            snapshot.gold = 1000000;
            CampaignState strong = rules.Restore(snapshot);

            Assert.AreEqual(-1, rules.JoinPrice(strong, sentinel), "no Diplomacy, no offer");
            CampaignState diplomat = WithSkills(strong, (SecondarySkill.Diplomacy, SkillLevel.Expert));
            int price = rules.JoinPrice(diplomat, sentinel);
            Assert.GreaterOrEqual(price, 0, "Expert Diplomacy convinces every willing army");
            int gold = diplomat.Gold;

            Assert.IsTrue(rules.Hire(diplomat, sentinel).IsSuccess);
            Assert.AreEqual(gold - price, diplomat.Gold);
            Assert.IsTrue(diplomat.IsCompleted(sentinel.Id));
            Assert.IsTrue(sentinel.Army.All(troop => diplomat.Army.Any(stack => stack.UnitId == troop.UnitId)));
            Assert.AreEqual(-1, rules.JoinPrice(diplomat, catalog.Mission.Encounter(EncounterKind.Boss)), "a commander never joins");
        }

        [Test]
        public void Surrender_PaysTheEnemyHeroAndKeepsTheSurvivors()
        {
            EncounterDefinition boss = catalog.Mission.Encounter(EncounterKind.Boss);
            Assume.That(boss.Commander, Is.Not.Null);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.gold = 100000;
            CampaignState state = rules.Restore(snapshot);
            List<Troop> survivors = new List<Troop> { new Troop(state.Army[0].UnitId, 1) };
            int price = rules.SurrenderPrice(state, survivors);
            CampaignState diplomat = WithSkills(state, (SecondarySkill.Diplomacy, SkillLevel.Expert));

            Assert.Less(rules.SurrenderPrice(diplomat, survivors), price, "Diplomacy makes surrender cheaper");
            Assert.IsFalse(rules.ApplySurrender(state, catalog.Mission.Encounter(EncounterKind.Sentinel), survivors, 0).IsSuccess, "armies without a hero take no gold");
            Assert.IsTrue(rules.ApplySurrender(state, boss, survivors, 3).IsSuccess);
            Assert.AreEqual(100000 - price, state.Gold);
            Assert.AreEqual(1, state.Army.Single().Count);
            Assert.AreEqual(3, state.Mana);
            Assert.IsFalse(state.IsCompleted(boss.Id));
        }

        private CampaignState WithExperience(CampaignState state, long experience)
        {
            CampaignSnapshot snapshot = state.Snapshot();
            snapshot.experience = (int)experience;
            return rules.Restore(snapshot);
        }

        private CampaignState WithSkills(CampaignState state, params (SecondarySkill Skill, SkillLevel Level)[] skills)
        {
            CampaignSnapshot snapshot = state.Snapshot();
            foreach ((SecondarySkill skill, SkillLevel level) in skills)
            {
                snapshot.skills.RemoveAll(known => known.skill == skill.ToString());
                snapshot.skills.Add(new SkillSnapshot { skill = skill.ToString(), level = (int)level });
            }

            return rules.Restore(snapshot);
        }
    }
}
