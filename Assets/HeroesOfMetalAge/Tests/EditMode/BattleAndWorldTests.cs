using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    public class BattleAndWorldTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
        }

        [Test]
        public void Defending_ReducesDamage()
        {
            UnitDefinition unit = catalog.Unit(catalog.Mission.RecruitmentIds[0]);
            CombatPolicy policy = catalog.Mission.Combat;
            int normal = DamageCalculator.Calculate(unit, 10, 2, unit, 2, false, true, 0.5, policy);
            int defended = DamageCalculator.Calculate(unit, 10, 2, unit, 2, true, true, 0.5, policy);

            Assert.Less(defended, normal);
        }

        [Test]
        public void RangedUnit_DealsLessInMelee()
        {
            UnitDefinition archer = catalog.Units.First(unit => unit.IsRanged);
            UnitDefinition target = catalog.Unit(catalog.Mission.RecruitmentIds[0]);
            CombatPolicy policy = catalog.Mission.Combat;

            Assert.Less(DamageCalculator.Calculate(archer, 10, 2, target, 2, false, true, 0.5, policy),
                DamageCalculator.Calculate(archer, 10, 2, target, 2, false, false, 0.5, policy));
        }

        [Test]
        public void Battle_UsesAuthoredHealth()
        {
            CampaignState campaign = rules.NewCampaign();
            BattleEngine battle = new BattleEngine(catalog, new SeededRandomSource(42), campaign, catalog.Mission.Encounter(EncounterKind.Sentinel));

            Assert.AreEqual(campaign.Army[0].Count * catalog.Unit(campaign.Army[0].UnitId).Health, battle.Combatants[0].TotalHealth);
        }

        [Test]
        public void InvalidBattleAction_LeavesBattleUnchanged()
        {
            BattleEngine battle = new BattleEngine(catalog, new SeededRandomSource(42), rules.NewCampaign(), catalog.Mission.Encounter(EncounterKind.Sentinel));
            string before = Fingerprint(battle);

            BattleResolution result = battle.Execute(new BattleAction(BattleActionKind.MoveOrAttack, new HexPosition(-1, -1)), battle.Active.IsPlayer);

            Assert.IsFalse(result.Result.IsSuccess);
            Assert.AreEqual(before, Fingerprint(battle));
        }

        [Test]
        public void WrongArmy_CannotTakeATurn()
        {
            BattleEngine battle = new BattleEngine(catalog, new SeededRandomSource(42), rules.NewCampaign(), catalog.Mission.Encounter(EncounterKind.Sentinel));

            Assert.IsFalse(battle.Execute(new BattleAction(BattleActionKind.Pass), !battle.Active.IsPlayer).Result.IsSuccess);
        }

        [Test]
        public void MissedStrike_DealsNoDamage()
        {
            // every roll is 0, below any positive miss chance, so every strike misses
            Assume.That(catalog.Mission.Combat.MissChance, Is.GreaterThan(0));
            BattleEngine battle = new BattleEngine(catalog, new FixedRandomSource(0), rules.NewCampaign(), catalog.Mission.Encounter(EncounterKind.Sentinel));
            int totalHealth = battle.Combatants.Sum(combatant => combatant.TotalHealth);
            BattleAi ai = new BattleAi();
            BattleResolution result = null;
            for (int turn = 0; turn < 50 && (result == null || result.Hits.Count == 0); turn++)
            {
                result = battle.Execute(ai.Choose(battle), battle.Active.IsPlayer);
                Assert.IsTrue(result.Result.IsSuccess);
            }

            Assert.IsTrue(result.Hits.Count > 0, "the stacks met within 50 turns");
            Assert.IsTrue(result.Hits.All(strike => strike.IsMiss && strike.Damage == 0 && strike.Casualties == 0));
            Assert.AreEqual(totalHealth, battle.Combatants.Sum(combatant => combatant.TotalHealth));
        }

        [Test]
        public void SameSeed_ReplaysTheSameBattle()
        {
            Assert.AreEqual(PlayOut(7), PlayOut(7));
        }

        [Test]
        public void HexDistance_IsSymmetric()
        {
            HexPosition a = new HexPosition(1, 1);
            HexPosition b = new HexPosition(2, 2);

            Assert.AreEqual(HexGrid.Distance(a, b), HexGrid.Distance(b, a));
        }

        [Test]
        public void Route_IsDeterministicAndBlockedBySites()
        {
            WorldMap world = new WorldMap(catalog.Mission);
            CampaignState fresh = rules.NewCampaign();
            WorldPosition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel).Position;

            CollectionAssert.AreEqual(world.FindRoute(fresh, sentinel), world.FindRoute(fresh, sentinel));
            Assert.Greater(world.FindRoute(fresh, sentinel).Count, 0);
            Assert.AreEqual(0, world.FindRoute(fresh, catalog.Mission.Encounter(EncounterKind.Town).Position).Count, "the unresolved sentinel blocks the town");
        }

        private string PlayOut(int seed)
        {
            BattleEngine battle = new BattleEngine(catalog, new SeededRandomSource(seed), rules.NewCampaign(), catalog.Mission.Encounter(EncounterKind.Sentinel));
            BattleAi ai = new BattleAi();
            for (int i = 0; i < 200 && battle.Outcome == BattleOutcome.InProgress; i++)
            {
                battle.Execute(ai.Choose(battle), battle.Active.IsPlayer);
            }

            return Fingerprint(battle) + "/" + battle.Outcome;
        }

        private static string Fingerprint(BattleEngine battle)
        {
            string stacks = string.Join("|", battle.Combatants.Select(stack => stack.Id + ":" + stack.TotalHealth + ":" + stack.Position));
            return stacks + "/" + battle.Round + "/" + (battle.Active != null ? battle.Active.Id : -1);
        }
    }
}
