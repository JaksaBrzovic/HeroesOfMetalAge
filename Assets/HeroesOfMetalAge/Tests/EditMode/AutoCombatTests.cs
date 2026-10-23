using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Auto combat, quick combat and the casualties the result screens show.</summary>
    public class AutoCombatTests
    {
        private const int MAX_TICKS = 2000;

        private DefinitionCatalog catalog;
        private EncounterDefinition sentinel;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
        }

        [Test]
        public void Casualties_CountTheCreaturesEachSideLost()
        {
            CampaignRules rules = new CampaignRules(catalog);
            BattleEngine battle = new BattleEngine(catalog, new SeededRandomSource(4), rules.NewCampaign(), sentinel);
            BattleAi ai = new BattleAi(catalog);
            for (int i = 0; i < 400 && battle.Outcome == BattleOutcome.InProgress; i++)
            {
                battle.Execute(ai.Choose(battle), battle.Active.IsPlayer);
            }

            foreach (bool isPlayer in new[] { true, false })
            {
                int lost = battle.Combatants.Where(combatant => combatant.IsPlayer == isPlayer).Sum(combatant => combatant.StartCount - combatant.Count);
                Assert.AreEqual(lost, battle.Casualties(isPlayer).Sum(troop => troop.Count));
                Assert.AreEqual(battle.Casualties(isPlayer).Count, battle.Casualties(isPlayer).Select(troop => troop.UnitId).Distinct().Count(), "one entry per unit");
            }
        }

        [Test]
        public void QuickCombat_GoesStraightToTheVictoryScreen()
        {
            GameSession session = AtSentinel(500);
            session.IsQuickCombat = true;

            session.FightEncounter();

            Assert.AreEqual(DialogKind.Victory, session.Dialog);
            Assert.AreEqual(GameScreen.Adventure, session.Screen, "the battlefield never opens");
            Assert.IsTrue(session.Campaign.IsCompleted(sentinel.Id));
            Assert.AreEqual(sentinel.Army.Sum(troop => troop.Count), session.Casualties.Enemy.Sum(troop => troop.Count), "every enemy fell");
            session.ContinueVictory();
            session.ContinueRewards();
            Assert.AreNotEqual(DialogKind.Victory, session.Dialog);
        }

        [Test]
        public void QuickCombat_Defeat_OpensTheDefeatScreenAndAllowsARetry()
        {
            GameSession session = AtSentinel(1);
            session.IsQuickCombat = true;
            int army = session.Campaign.Army.Sum(troop => troop.Count);

            session.FightEncounter();

            Assert.AreEqual(DialogKind.Defeat, session.Dialog);
            Assert.AreNotEqual(GameScreen.Battle, session.Screen);
            Assert.AreEqual(army, session.Casualties.Player.Sum(troop => troop.Count));
            Assert.AreEqual(army, session.Campaign.Army.Sum(troop => troop.Count), "a defeat writes no losses");
            session.FightEncounter();
            Assert.AreEqual(DialogKind.Defeat, session.Dialog, "the retry is quick too");
        }

        [Test]
        public void QuickCombat_RetryAfterABattlefieldDefeat_LeavesTheBattlefield()
        {
            GameSession session = AtSentinel(1);
            session.FightEncounter();
            Tick(session, () => session.IsBusy);
            session.ToggleAutoCombat();
            Tick(session, () => session.Dialog == DialogKind.None);
            Assert.AreEqual(DialogKind.Defeat, session.Dialog);
            Assert.AreEqual(GameScreen.Battle, session.Screen);

            session.IsQuickCombat = true;
            session.FightEncounter();

            Assert.AreEqual(DialogKind.Defeat, session.Dialog);
            Assert.AreEqual(GameScreen.Adventure, session.Screen);
        }

        [Test]
        public void AutoCombat_PlaysThePlayersSideToTheEnd()
        {
            GameSession session = AtSentinel(500);
            session.FightEncounter();
            Tick(session, () => session.IsBusy);

            session.ToggleAutoCombat();
            Assert.IsTrue(session.IsAutoCombat);
            Assert.IsTrue(session.IsBusy, "the player's commands wait while the computer fights");
            Tick(session, () => session.Screen == GameScreen.Battle);

            Assert.AreEqual(DialogKind.Victory, session.Dialog);
            Assert.IsFalse(session.IsAutoCombat, "auto combat ends with the battle");
            Assert.Greater(session.Casualties.Enemy.Count, 0);
        }

        [Test]
        public void AutoCombat_CanBeStopped()
        {
            GameSession session = AtSentinel(50);
            session.FightEncounter();
            Tick(session, () => session.IsBusy);
            session.ToggleAutoCombat();
            session.ToggleAutoCombat();

            Assert.IsFalse(session.IsAutoCombat);
            Tick(session, () => session.IsBusy);
            Assert.IsTrue(session.Battle.Active.IsPlayer, "the player commands again");
        }

        // a session whose hero stands next to the sentinel with the given number of each of his stacks, in the sentinel dialog
        private GameSession AtSentinel(int count)
        {
            CampaignRules rules = new CampaignRules(catalog);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            WorldPosition next = NextTo(rules, sentinel.Position);
            snapshot.x = next.X;
            snapshot.y = next.Y;
            snapshot.skills.Clear();
            foreach (StackSnapshot stack in snapshot.army)
            {
                stack.count = count;
            }

            GameSession session = CampaignTestUtility.NewSession(catalog, new MemorySaveStore { Snapshot = snapshot });
            session.Load();
            session.Plan(sentinel.Position);
            session.Travel();
            Tick(session, () => session.IsBusy);
            Assert.AreEqual(DialogKind.Hostile, session.Dialog);
            return session;
        }

        private static void Tick(GameSession session, System.Func<bool> isWaiting)
        {
            for (int i = 0; i < MAX_TICKS && isWaiting(); i++)
            {
                session.Tick(1);
            }

            Assert.IsFalse(isWaiting(), "the session settles");
        }

        private WorldPosition NextTo(CampaignRules rules, WorldPosition position)
        {
            WorldMap map = new WorldMap(catalog.Mission);
            CampaignState fresh = rules.NewCampaign();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldPosition cell = new WorldPosition(position.X + dx, position.Y + dy);
                    if (cell != position && map.IsWalkable(cell) && map.CanStep(cell, position) && map.ObjectAt(fresh, cell) == null && map.Site(fresh, cell) == null)
                    {
                        return cell;
                    }
                }
            }

            throw new AssertionException("No free cell next to " + position);
        }
    }
}
