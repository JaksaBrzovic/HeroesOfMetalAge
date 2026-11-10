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

        [Test]
        public void RestartBattle_BeginsTheBattleAgainWithTheFullArmy()
        {
            GameSession session = AtSentinel(50);
            session.FightEncounter();
            Tick(session, () => session.IsBusy);
            session.ToggleAutoCombat();
            for (int i = 0; i < 6 && session.Battle.Outcome == BattleOutcome.InProgress; i++)
            {
                session.Tick(1);
            }

            Assume.That(session.Battle.Outcome == BattleOutcome.InProgress, "the battle is still undecided");
            BattleEngine first = session.Battle;
            session.IsQuickCombat = true;

            session.RestartBattle();

            Assert.AreNotSame(first, session.Battle);
            Assert.AreEqual(GameScreen.Battle, session.Screen, "a restart stays on the battlefield even with quick combat on");
            Assert.IsFalse(session.IsAutoCombat);
            Assert.IsTrue(session.Battle.Combatants.All(combatant => combatant.Count == combatant.StartCount), "every stack is whole again");
            Assert.AreEqual(session.Campaign.Army.Sum(troop => troop.Count), session.Battle.Combatants.Where(combatant => combatant.IsPlayer).Sum(combatant => combatant.Count));
        }

        [Test]
        public void AStackThatMoves_StrikesWhenItArrives()
        {
            GameSession session = AtSentinel(50);
            int published = 0;
            session.DamageResolved += hit => published++;
            session.FightEncounter();
            session.ToggleAutoCombat();
            bool wasPending = false;
            int publishedBefore = 0;
            float pendingSeconds = 0f;
            for (int i = 0; i < 20000 && session.Screen == GameScreen.Battle && session.Dialog == DialogKind.None; i++)
            {
                session.Tick(0.05f);
                if (session.PendingStriker != null)
                {
                    if (!wasPending)
                    {
                        publishedBefore = published;
                        pendingSeconds = 0f;
                    }

                    Assert.AreEqual(publishedBefore, published, "no blow is shown before the stack arrives");
                    Assert.IsTrue(session.IsBusy, "the next stack waits for the blow");
                    wasPending = true;
                    pendingSeconds += 0.05f;
                }
                else if (wasPending)
                {
                    Assert.Greater(published, publishedBefore, "the blows land on arrival");
                    Assert.GreaterOrEqual(pendingSeconds, 1f / session.BattleMoveHexesPerSecond - 0.06f, "the glide takes at least one hex");
                    return;
                }
            }

            Assert.Fail("no stack moved before it struck");
        }

        [Test]
        public void QuickBattleAnimation_UsesTheQuickPace()
        {
            GameSession session = AtSentinel(50);
            float normal = session.BattleMoveHexesPerSecond;

            session.IsQuickBattleAnimation = true;

            Assert.AreEqual(2f, normal, "the test session's normal pace");
            Assert.AreEqual(6f, session.BattleMoveHexesPerSecond);
        }

        [Test]
        public void RestartBattle_DoesNothingOutsideAnUndecidedBattle()
        {
            GameSession session = AtSentinel(50);

            session.RestartBattle();

            Assert.AreEqual(DialogKind.Hostile, session.Dialog);
            Assert.IsNull(session.Battle);
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
