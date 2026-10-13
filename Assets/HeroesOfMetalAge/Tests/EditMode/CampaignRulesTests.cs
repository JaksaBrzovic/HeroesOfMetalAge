using System;
using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace HeroesOfMetalAge.Tests
{
    public class CampaignRulesTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private string firstUnitId;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            firstUnitId = catalog.Mission.RecruitmentIds[0];
        }

        [Test]
        public void NewCampaign_RoundTripsThroughSnapshot()
        {
            CampaignState state = rules.NewCampaign();

            Assert.AreEqual(state.HeroId, rules.Restore(state.Snapshot()).HeroId);
        }

        [Test]
        public void Buy_AwayFromTown_IsRejected()
        {
            Assert.IsFalse(rules.Buy(rules.NewCampaign(), firstUnitId, 1).IsSuccess);
        }

        [Test]
        public void Buy_ConservesGoldStockAndTroops()
        {
            CampaignState state = CampaignTestUtility.AtTown(rules, rules.NewCampaign(), catalog);
            int gold = state.Gold;
            int stock = state.Stock(firstUnitId);
            int troops = state.Army.Where(troop => troop.UnitId == firstUnitId).Sum(troop => troop.Count);

            Assert.IsTrue(rules.Buy(state, firstUnitId, 2).IsSuccess);

            Assert.AreEqual(gold - 2 * rules.Price(state, firstUnitId), state.Gold);
            Assert.AreEqual(stock - 2, state.Stock(firstUnitId));
            Assert.AreEqual(troops + 2, state.Army.First(troop => troop.UnitId == firstUnitId).Count);
        }

        [Test]
        public void Buy_RejectedForStockOrGold_LeavesCampaignUnchanged()
        {
            CampaignState state = CampaignTestUtility.AtTown(rules, rules.NewCampaign(), catalog);
            string before = JsonUtility.ToJson(state.Snapshot());
            Assert.IsFalse(rules.Buy(state, firstUnitId, state.Stock(firstUnitId) + 1).IsSuccess);
            Assert.AreEqual(before, JsonUtility.ToJson(state.Snapshot()));

            CampaignSnapshot poor = state.Snapshot();
            poor.gold = 0;
            state = rules.Restore(poor);
            before = JsonUtility.ToJson(state.Snapshot());
            Assert.IsFalse(rules.Buy(state, firstUnitId, 1).IsSuccess);
            Assert.AreEqual(before, JsonUtility.ToJson(state.Snapshot()));
        }

        [Test]
        public void Buy_InvalidUnitOrCount_IsRejected()
        {
            CampaignState state = CampaignTestUtility.AtTown(rules, rules.NewCampaign(), catalog);

            Assert.IsFalse(rules.Buy(state, "missing", 1).IsSuccess);
            Assert.IsFalse(rules.Buy(state, firstUnitId, -1).IsSuccess);
        }

        [Test]
        public void EndDay_GrowsStockOnlyAtWeekBoundaries()
        {
            CampaignState state = rules.NewCampaign();
            int stock = state.Stock(firstUnitId);
            int growth = catalog.Unit(firstUnitId).WeeklyGrowth;
            for (int day = 0; day < catalog.Mission.DaysPerWeek - 1; day++)
            {
                Assert.IsTrue(rules.EndDay(state, out bool isNewWeek).IsSuccess);
                Assert.IsFalse(isNewWeek, "no early growth on day " + day);
            }

            Assert.IsTrue(rules.EndDay(state, out bool isFirstWeek).IsSuccess && isFirstWeek);
            Assert.AreEqual(stock + growth, state.Stock(firstUnitId));
            for (int day = 0; day < catalog.Mission.DaysPerWeek; day++)
            {
                rules.EndDay(state, out _);
            }

            Assert.AreEqual(stock + growth * 2, state.Stock(firstUnitId));
            Assert.AreEqual(rules.DailyMovement(state), state.Movement, "movement restored");
        }

        [Test]
        public void ForeignGarrison_CannotBeTransferred()
        {
            CampaignState state = CampaignTestUtility.AtTown(rules, rules.NewCampaign(), catalog);

            Assert.IsFalse(rules.Transfer(state, false, 0).IsSuccess);
        }

        [Test]
        public void OwnedTown_UsesDiscountAndTransfersConserveTroops()
        {
            CampaignState state = CampaignTestUtility.OwnedTown(rules, rules.NewCampaign(), catalog);
            int total = state.Army.Sum(troop => troop.Count);

            Assert.AreEqual(Math.Ceiling(catalog.Unit(firstUnitId).Cost * catalog.Mission.OwnedPriceMultiplier), rules.Price(state, firstUnitId));
            Assert.IsTrue(rules.Transfer(state, false, 0).IsSuccess);
            Assert.AreEqual(total, state.Army.Sum(troop => troop.Count) + state.Garrison.Sum(troop => troop.Count));
            Assert.IsTrue(rules.Transfer(state, true, 0).IsSuccess);
        }

        [Test]
        public void StationedHero_BlocksRecruitingAndSurvivesSaves()
        {
            CampaignState state = CampaignTestUtility.OwnedTown(rules, rules.NewCampaign(), catalog);

            Assert.IsTrue(rules.StationHero(state).IsSuccess && state.IsHeroHome && state.Army.Count == 0);
            Assert.IsFalse(rules.Transfer(state, true, 0).IsSuccess);
            Assert.IsFalse(rules.Buy(state, firstUnitId, 1).IsSuccess);
            Assert.IsTrue(rules.Restore(state.Snapshot()).IsHeroHome);
            Assert.IsTrue(rules.StationHero(state).IsSuccess && !state.IsHeroHome);
        }

        [Test]
        public void StationHero_CapacityFailure_LeavesBothArmiesUnchanged()
        {
            CampaignState state = CampaignTestUtility.OwnedTown(rules, rules.NewCampaign(), catalog);
            CampaignSnapshot full = state.Snapshot();
            full.army = catalog.Units.Select(unit => new StackSnapshot { unitId = unit.Id, count = CampaignRules.MAXIMUM_STACK_COUNT }).ToList();
            full.garrison = catalog.Units.Take(1).Select(unit => new StackSnapshot { unitId = unit.Id, count = 1 }).ToList();
            state = rules.Restore(full);
            string before = JsonUtility.ToJson(state.Snapshot());

            Assert.IsFalse(rules.StationHero(state).IsSuccess);
            Assert.AreEqual(before, JsonUtility.ToJson(state.Snapshot()));
        }

        [Test]
        public void Equipment_AppliesAndReversesAuthoredBonuses()
        {
            CampaignState state = rules.NewCampaign();
            rules.DebugArtefacts(state);
            ArtefactDefinition weapon = catalog.Artefacts.First(artefact => artefact.Slot == EquipmentSlot.Weapon);
            int attack = rules.Attack(state);
            int defense = rules.Defense(state);

            Assert.IsTrue(rules.Equip(state, state.Inventory.ToList().IndexOf(weapon.Id)).IsSuccess);
            Assert.AreEqual(attack + weapon.AttackBonus, rules.Attack(state));
            Assert.AreEqual(defense + weapon.DefenseBonus, rules.Defense(state));
            Assert.IsTrue(rules.Unequip(state, EquipmentSlot.Weapon).IsSuccess);
            Assert.AreEqual(attack, rules.Attack(state));
        }

        [Test]
        public void Level_FollowsAuthoredExperienceThreshold()
        {
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.experience = catalog.Hero(snapshot.heroId).ExperiencePerLevel;

            Assert.AreEqual(2, rules.Level(rules.Restore(snapshot)));
        }

        [Test]
        public void Victory_IsAppliedOnce()
        {
            CampaignState state = rules.NewCampaign();
            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            int gold = state.Gold;
            VictoryRewards rewards = rules.RollRewards(state, sentinel, new SeededRandomSource(3), 0);

            Assert.IsTrue(rules.ApplyVictory(state, sentinel, state.Army.ToList(), rewards).IsSuccess);
            Assert.IsFalse(rules.ApplyVictory(state, sentinel, state.Army.ToList(), rewards).IsSuccess);
            Assert.AreEqual(gold + rewards.Gold, state.Gold);
            CollectionAssert.AreEqual(rewards.ArtefactIds, state.Inventory);
        }

        [Test]
        public void RolledRewards_AlwaysPayGoldAndAtMostOneArtefact()
        {
            CampaignState state = rules.NewCampaign();
            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            RewardPolicy policy = catalog.Mission.Rewards;
            SeededRandomSource random = new SeededRandomSource(11);
            bool sawNone = false;
            bool sawOne = false;
            for (int i = 0; i < 200; i++)
            {
                VictoryRewards rewards = rules.RollRewards(state, sentinel, random, 0);
                Assert.That(rewards.Gold, Is.InRange((int)Math.Floor(sentinel.RewardGold * (1 - policy.GoldVariation)),
                    (int)Math.Ceiling(sentinel.RewardGold * (1 + policy.GoldVariation))));
                Assert.LessOrEqual(rewards.ArtefactIds.Count, 1);
                sawNone |= rewards.ArtefactIds.Count == 0;
                sawOne |= rewards.ArtefactIds.Count == 1;
            }

            Assert.IsTrue(sawNone && sawOne, "victories sometimes bring no artefact and sometimes one");
        }

        [Test]
        public void RolledRewards_GuaranteeALegendaryWithinEveryRunOfVictories()
        {
            CampaignState state = rules.NewCampaign();
            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            int guarantee = catalog.Mission.Rewards.LegendaryGuaranteeVictories;
            Assert.Greater(guarantee, 0);
            SeededRandomSource random = new SeededRandomSource(5);
            int withoutLegendary = 0;
            for (int i = 0; i < 400; i++)
            {
                VictoryRewards rewards = rules.RollRewards(state, sentinel, random, withoutLegendary);
                withoutLegendary = rewards.HasLegendary ? 0 : withoutLegendary + 1;
                Assert.Less(withoutLegendary, guarantee, "a legendary artefact is forced at the latest on every " + guarantee + "th victory");
            }

            VictoryRewards due = rules.RollRewards(state, sentinel, random, guarantee - 1);
            Assert.IsTrue(due.HasLegendary && catalog.Artefact(due.ArtefactIds.Single()).Rarity == ArtefactRarity.Legendary);
        }

        [Test]
        public void UnknownRewardArtefact_IsRejectedWithoutChanges()
        {
            CampaignState state = rules.NewCampaign();
            EncounterDefinition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            int gold = state.Gold;

            Assert.IsFalse(rules.ApplyVictory(state, sentinel, state.Army.ToList(), new VictoryRewards(100, new[] { "missing" }, false)).IsSuccess);
            Assert.AreEqual(gold, state.Gold);
            Assert.IsFalse(state.IsCompleted(sentinel.Id));
        }

        [Test]
        public void ReorderedDatabase_KeepsSavedIdentities()
        {
            CampaignState state = rules.NewCampaign();
            DefinitionCatalog reordered = new DefinitionCatalog(catalog.Units.Reverse(), catalog.Artefacts.Reverse(), catalog.Heroes, catalog.Mission);

            Assert.AreEqual(state.Army[0].UnitId, new CampaignRules(reordered).Restore(state.Snapshot()).Army[0].UnitId);
        }

        [Test]
        public void Restore_UnknownContent_IsRejected()
        {
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.army[0].unitId = "missing";

            Assert.Throws<ArgumentException>(() => rules.Restore(snapshot));
        }
    }
}
