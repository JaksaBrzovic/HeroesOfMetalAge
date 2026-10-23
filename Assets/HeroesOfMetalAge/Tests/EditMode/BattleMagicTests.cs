using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Spells, luck, the battle skills, Tactics and enemy commanders in battle, and casting through the session.</summary>
    public class BattleMagicTests
    {
        // high enough that no strike misses, no morale or luck shows and every damage roll is the maximum
        private const double NO_CHANCE = 0.99;

        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private EncounterDefinition sentinel;
        private EncounterDefinition boss;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel);
            boss = catalog.Mission.Encounter(EncounterKind.Boss);
        }

        [Test]
        public void DamageSpell_DealsItsAmountRaisedBySorcery()
        {
            SpellDefinition arrow = catalog.Spell("magic-arrow");
            CampaignState state = Magician((SecondarySkill.Sorcery, SkillLevel.Expert));
            BattleEngine battle = PlayerTurn(state, sentinel, new FixedRandomSource(NO_CHANCE));
            Combatant enemy = battle.Combatants.First(combatant => !combatant.IsPlayer);
            int health = enemy.TotalHealth;
            int mana = battle.Player.Mana;

            BattleResolution cast = battle.Execute(new BattleAction(BattleActionKind.CastSpell, enemy.Position, arrow.Id), true);

            Assert.IsTrue(cast.Result.IsSuccess, cast.Result.Error);
            int expected = (int)System.Math.Round(arrow.Amount(rules.SpellPower(state)) * (1 + catalog.HeroPolicy.Value(SecondarySkill.Sorcery, SkillLevel.Expert)),
                System.MidpointRounding.ToEven);
            Assert.AreEqual(expected, cast.Spell.Hits.Single().Damage);
            Assert.AreEqual(System.Math.Max(0, health - expected), enemy.TotalHealth);
            Assert.AreEqual(mana - arrow.ManaCost, battle.Player.Mana);
            Assert.IsTrue(battle.Active.IsPlayer, "casting does not use up the stack's turn");
        }

        [Test]
        public void Hero_CastsOncePerRound_AndNeedsMana()
        {
            CampaignState state = Magician();
            BattleEngine battle = PlayerTurn(state, sentinel, new FixedRandomSource(NO_CHANCE));
            Combatant own = battle.Combatants.First(combatant => combatant.IsPlayer);

            Assert.IsTrue(battle.Execute(new BattleAction(BattleActionKind.CastSpell, own.Position, "haste"), true).Result.IsSuccess);
            Assert.IsFalse(battle.Execute(new BattleAction(BattleActionKind.CastSpell, own.Position, "bless"), true).Result.IsSuccess, "one spell per round");

            CampaignSnapshot poor = Magician().Snapshot();
            poor.mana = 0;
            BattleEngine broke = PlayerTurn(rules.Restore(poor), sentinel, new FixedRandomSource(NO_CHANCE));
            Assert.IsFalse(broke.CanCast(true, "haste", out string reason));
            Assert.AreEqual("Not enough mana.", reason);
        }

        [Test]
        public void LastingSpell_ChangesTheStackAndExpires()
        {
            SpellDefinition haste = catalog.Spell("haste");
            CampaignState state = Magician();
            BattleEngine battle = PlayerTurn(state, sentinel, new FixedRandomSource(NO_CHANCE));
            Combatant own = battle.Active;
            int speed = battle.Speed(own);

            battle.Execute(new BattleAction(BattleActionKind.CastSpell, own.Position, haste.Id), true);

            Assert.AreEqual(speed + haste.Amount(rules.SpellPower(state)), battle.Speed(own));
            int rounds = haste.Duration(rules.SpellPower(state));
            int startRound = battle.Round;
            BattleAi ai = new BattleAi(catalog);
            for (int i = 0; i < 200 && battle.Outcome == BattleOutcome.InProgress && battle.Round < startRound + rounds; i++)
            {
                battle.Execute(battle.Active.IsPlayer ? new BattleAction(BattleActionKind.Defend) : ai.Choose(battle), battle.Active.IsPlayer);
            }

            Assume.That(battle.Outcome, Is.EqualTo(BattleOutcome.InProgress));
            Assert.IsFalse(own.HasSpell(haste.Id), "the spell ends after its rounds");
        }

        [Test]
        public void Resistance_LetsAHostileSpellFail()
        {
            Assume.That(boss.Commander, Is.Not.Null);
            CampaignState state = WithSkills(rules.NewCampaign(), (SecondarySkill.Resistance, SkillLevel.Expert));
            // every roll 0: the resistance roll is below any chance
            BattleEngine battle = new BattleEngine(catalog, new FixedRandomSource(0), state, boss);
            PassUntil(battle, false);
            Combatant own = battle.Combatants.First(combatant => combatant.IsPlayer && combatant.IsAlive);
            int health = own.TotalHealth;

            BattleResolution cast = battle.Execute(new BattleAction(BattleActionKind.CastSpell, own.Position, "magic-arrow"), false);

            Assert.IsTrue(cast.Result.IsSuccess, cast.Result.Error);
            Assert.IsTrue(cast.Spell.Hits.Single().IsResisted);
            Assert.AreEqual(health, own.TotalHealth);
        }

        [Test]
        public void Commander_CastsItsSpellsThroughTheAi()
        {
            Assume.That(boss.Commander, Is.Not.Null);
            BattleEngine battle = new BattleEngine(catalog, new FixedRandomSource(NO_CHANCE), rules.NewCampaign(), boss);
            PassUntil(battle, false);
            int mana = battle.Enemy.Mana;

            BattleAction action = new BattleAi(catalog).Choose(battle);

            Assert.AreEqual(BattleActionKind.CastSpell, action.Kind);
            Assert.IsTrue(battle.Execute(action, false).Result.IsSuccess);
            Assert.Less(battle.Enemy.Mana, mana);
            Assert.AreNotEqual(BattleActionKind.CastSpell, new BattleAi(catalog).Choose(battle).Kind, "then the stack acts");
        }

        [Test]
        public void Tactics_LetsThePlayerPlaceTheStacksFirst()
        {
            CampaignState state = WithSkills(rules.NewCampaign(), (SecondarySkill.Tactics, SkillLevel.Expert));
            BattleEngine battle = new BattleEngine(catalog, new FixedRandomSource(NO_CHANCE), state, sentinel);

            Assert.IsTrue(battle.IsTacticsPhase);
            Assert.AreEqual(0, battle.Round);
            Assert.IsTrue(battle.Active.IsPlayer);
            HexPosition free = battle.ReachableCells().First(cell => battle.At(cell) == null);
            Assert.Less(free.X, battle.TacticsColumns);
            Combatant stack = battle.Active;
            Assert.IsTrue(battle.Execute(new BattleAction(BattleActionKind.MoveOrAttack, free), true).Result.IsSuccess);
            Assert.AreEqual(free, stack.Position);
            Assert.IsFalse(battle.Execute(new BattleAction(BattleActionKind.MoveOrAttack, new HexPosition(battle.Policy.Width - 1, 0)), true).Result.IsSuccess,
                "outside the player's columns");

            Assert.IsTrue(battle.Execute(new BattleAction(BattleActionKind.EndTactics), true).Result.IsSuccess);
            Assert.IsFalse(battle.IsTacticsPhase);
            Assert.AreEqual(1, battle.Round);
        }

        [Test]
        public void GoodLuck_DoublesTheBlow()
        {
            // archers shoot from anywhere and nobody strikes back; without skills no morale is rolled
            UnitDefinition archer = catalog.Units.First(unit => unit.IsRanged);
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.luck = CampaignRules.MAXIMUM_LUCK;
            snapshot.skills.Clear();
            snapshot.army = new System.Collections.Generic.List<StackSnapshot> { new StackSnapshot { unitId = archer.Id, count = 10 } };
            CampaignState state = rules.Restore(snapshot);
            Assume.That(catalog.HeroPolicy.LuckChance, Is.GreaterThan(0));
            // the miss roll, the luck roll (0 is always lucky), then the damage roll
            BattleEngine battle = new BattleEngine(catalog, new SequenceRandomSource(NO_CHANCE), state, sentinel);
            PassUntil(battle, true);
            BattleEngine lucky = new BattleEngine(catalog, new SequenceRandomSource(NO_CHANCE, 0, NO_CHANCE), state, sentinel);
            PassUntil(lucky, true);
            Combatant target = lucky.Combatants.First(combatant => !combatant.IsPlayer);
            Assert.IsTrue(lucky.CanAttack(target), "archers reach any enemy");

            DamageEvent hit = lucky.Execute(new BattleAction(BattleActionKind.MoveOrAttack, target.Position), true).Hits[0];
            DamageEvent plain = battle.Execute(new BattleAction(BattleActionKind.MoveOrAttack, target.Position), true).Hits[0];

            Assert.AreEqual(1, hit.Luck);
            Assert.AreEqual(0, plain.Luck, "the luck roll of 0.99 is above any chance");
            Assert.Greater(hit.Damage, plain.Damage);
        }

        [Test]
        public void ArmorerAndOffense_ScaleTheDamage()
        {
            UnitDefinition unit = catalog.Units[0];
            CombatPolicy policy = catalog.Mission.Combat;
            int plain = DamageCalculator.Calculate(unit, 100, 0, unit, 0, false, true, 1, policy);
            int offense = DamageCalculator.Calculate(unit, 100, 0, unit, 0, false, true, 1, policy, new StrikeModifiers(0.3, 0, 0, 0, false, false));
            int armored = DamageCalculator.Calculate(unit, 100, 0, unit, 0, false, true, 1, policy, new StrikeModifiers(0, 0.15, 0, 0, false, false));
            int cursed = DamageCalculator.Calculate(unit, 100, 0, unit, 0, false, true, 1, policy, new StrikeModifiers(0, 0, 0, 0, false, true));

            Assert.AreEqual(System.Math.Round(plain * 1.3), offense, 1);
            Assert.AreEqual(System.Math.Round(plain * 0.85), armored, 1);
            Assume.That(unit.MinimumDamage, Is.LessThan(unit.MaximumDamage));
            Assert.Less(cursed, plain, "a curse fixes the roll at the minimum");
        }

        [Test]
        public void Session_CastsAPickedSpellAtTheClickedHex()
        {
            GameSession session = CampaignTestUtility.NewSession(catalog, new MemorySaveStore());
            session.StartMission();
            session.DebugSpells();
            CampaignSnapshot snapshot = session.Campaign.Snapshot();
            WorldPosition next = NextTo(sentinel.Position);
            snapshot.x = next.X;
            snapshot.y = next.Y;
            MemorySaveStore store = new MemorySaveStore { Snapshot = snapshot };
            session = CampaignTestUtility.NewSession(catalog, store);
            session.Load();
            session.Plan(sentinel.Position);
            session.Travel();
            for (int i = 0; i < 50 && session.IsBusy; i++)
            {
                session.Tick(1);
            }

            Assert.AreEqual(DialogKind.Hostile, session.Dialog);
            session.FightEncounter();
            for (int i = 0; i < 50 && (session.IsBusy || !session.Battle.Active.IsPlayer); i++)
            {
                session.Tick(1);
            }

            session.OpenSpellBook();
            Assert.AreEqual(DialogKind.SpellBook, session.Dialog);
            session.SelectSpell("magic-arrow");
            Assert.AreEqual(DialogKind.None, session.Dialog);
            Assert.AreEqual("magic-arrow", session.TargetingSpellId);
            Combatant enemy = session.Battle.Combatants.First(combatant => !combatant.IsPlayer);
            session.CastAt(enemy.Position);

            Assert.IsNull(session.TargetingSpellId);
            Assert.IsTrue(session.Battle.Player.HasCastThisRound);
        }

        // the hero with every spell, full mana and the given skills
        private CampaignState Magician(params (SecondarySkill Skill, SkillLevel Level)[] skills)
        {
            CampaignState state = WithSkills(rules.NewCampaign(), skills);
            rules.DebugSpells(state);
            return state;
        }

        private BattleEngine PlayerTurn(CampaignState state, EncounterDefinition encounter, IRandomSource random)
        {
            BattleEngine battle = new BattleEngine(catalog, random, state, encounter);
            PassUntil(battle, true);
            return battle;
        }

        // the stacks pass until one of the wanted side has its turn
        private static void PassUntil(BattleEngine battle, bool isPlayer)
        {
            for (int i = 0; i < 50 && battle.Active.IsPlayer != isPlayer; i++)
            {
                battle.Execute(new BattleAction(BattleActionKind.Pass), battle.Active.IsPlayer);
            }

            Assert.AreEqual(isPlayer, battle.Active.IsPlayer);
        }

        private WorldPosition NextTo(WorldPosition position)
        {
            WorldMap map = new WorldMap(catalog.Mission);
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldPosition cell = new WorldPosition(position.X + dx, position.Y + dy);
                    CampaignState fresh = rules.NewCampaign();
                    if (cell != position && map.IsWalkable(cell) && map.CanStep(cell, position) && map.ObjectAt(fresh, cell) == null && map.Site(fresh, cell) == null)
                    {
                        return cell;
                    }
                }
            }

            throw new AssertionException("No free cell next to " + position);
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
