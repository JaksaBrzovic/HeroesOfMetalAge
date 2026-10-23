using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The adventure map's objects, the gate and its key, morale, the mission's goals and the exit portal.</summary>
    public class MapObjectTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private MapObjectRules objects;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            objects = new MapObjectRules(catalog, rules);
        }

        [Test]
        public void GoldPile_IsTakenOnceAndLeavesTheMap()
        {
            MapObjectDefinition pile = First(MapObjectKind.GoldPile);
            CampaignState state = At(pile.Position);
            int gold = state.Gold;

            Assert.IsTrue(objects.Visit(state, pile, out VisitOutcome outcome).IsSuccess);
            Assert.AreEqual(gold + pile.Gold, state.Gold);
            Assert.AreEqual(pile.Gold, outcome.Gold);
            Assert.IsNull(new WorldMap(catalog.Mission).ObjectAt(state, pile.Position), "a taken pile is gone");
            Assert.IsFalse(objects.Visit(state, pile, out _).IsSuccess, "it cannot be taken twice");
            Assert.AreEqual(gold + pile.Gold, state.Gold);
        }

        [Test]
        public void Chest_GivesGoldOrExperienceOnce()
        {
            MapObjectDefinition chest = First(MapObjectKind.TreasureChest);
            CampaignState forGold = At(chest.Position);
            CampaignState forExperience = At(chest.Position);
            int gold = forGold.Gold;

            Assert.IsTrue(objects.OpenChest(forGold, chest, true, out _).IsSuccess);
            Assert.IsTrue(objects.OpenChest(forExperience, chest, false, out _).IsSuccess);
            Assert.AreEqual(gold + chest.Gold, forGold.Gold);
            Assert.AreEqual(0, forGold.Experience);
            Assert.AreEqual(chest.Experience, forExperience.Experience);
            Assert.AreEqual(gold, forExperience.Gold);
            Assert.IsFalse(objects.OpenChest(forGold, chest, false, out _).IsSuccess, "an opened chest is empty");
        }

        [Test]
        public void Shrines_RaiseStatsForGoodAndMoraleUntilTheNextVictory()
        {
            MapObjectDefinition arena = First(MapObjectKind.AttackShrine);
            MapObjectDefinition horns = First(MapObjectKind.MoraleShrine);
            CampaignState state = At(arena.Position);
            int attack = rules.Attack(state);
            objects.Visit(state, arena, out _);
            objects.Visit(state, arena, out VisitOutcome again);
            Assert.AreEqual(attack + arena.AttackBonus, rules.Attack(state), "a shrine teaches once");
            Assert.IsFalse(again.IsRewarding);

            state = At(horns.Position);
            Assert.AreEqual(0, rules.Morale(state));
            objects.Visit(state, horns, out _);
            Assert.AreEqual(horns.MoraleBonus, rules.Morale(state));
            EncounterDefinition bandits = catalog.Mission.Encounters.First(encounter => encounter.IsHostile);
            Assert.IsTrue(rules.ApplyVictory(state, bandits, state.Army, new VictoryRewards(1, new string[0], false)).IsSuccess);
            Assert.AreEqual(0, rules.Morale(state), "the blessing lasts until the next won battle");
        }

        [Test]
        public void Mill_PaysOncePerWeek()
        {
            MapObjectDefinition mill = First(MapObjectKind.Mill);
            CampaignState state = At(mill.Position);
            int gold = state.Gold;
            objects.Visit(state, mill, out _);
            objects.Visit(state, mill, out _);
            Assert.AreEqual(gold + mill.Gold, state.Gold);

            for (int day = 0; day < catalog.Mission.DaysPerWeek; day++)
            {
                rules.EndDay(state, out _);
            }

            objects.Visit(state, mill, out _);
            Assert.AreEqual(gold + 2 * mill.Gold, state.Gold, "a new week, a new tithe");
        }

        [Test]
        public void Dwelling_SellsFromItsOwnStockAtFullPrice()
        {
            MapObjectDefinition camp = First(MapObjectKind.Dwelling);
            CampaignState state = At(camp.Position);
            int gold = state.Gold;
            int stock = state.DwellingStock(camp.Id);
            int count = objects.AffordableAtDwelling(state, camp);
            Assert.Greater(count, 0);

            Assert.IsTrue(objects.Recruit(state, camp, count).IsSuccess);
            Assert.AreEqual(gold - count * catalog.Unit(camp.UnitId).Cost, state.Gold);
            Assert.AreEqual(stock - count, state.DwellingStock(camp.Id));
            Assert.IsFalse(objects.Recruit(state, camp, state.DwellingStock(camp.Id) + 1).IsSuccess);
        }

        [Test]
        public void RedTent_OpensOnlyForTheQuestGiversKey()
        {
            MapObjectDefinition tent = First(MapObjectKind.BorderGate);
            MapObjectDefinition skald = First(MapObjectKind.QuestGiver);
            CampaignState state = At(new WorldPosition(tent.Position.X - 1, tent.Position.Y));
            WorldMap map = new WorldMap(catalog.Mission);
            MapObjectDefinition maiden = First(MapObjectKind.Prison);
            // the tent, not the fog of war, must be what bars the way
            rules.DebugRevealMap(state);

            Assert.IsFalse(objects.Visit(state, tent, out _).IsSuccess, "no key, no passage");
            Assert.AreEqual(0, map.FindRoute(state, maiden.Position).Count, "the tent bars the maiden's pocket");

            CampaignState withKey = At(skald.Position);
            objects.Visit(withKey, skald, out VisitOutcome given);
            Assert.AreEqual(tent.KeyId, given.KeyId);
            CampaignState snapshot = rules.Restore(Snapshot(withKey, new WorldPosition(tent.Position.X - 1, tent.Position.Y)));
            Assert.IsTrue(objects.Visit(snapshot, tent, out _).IsSuccess);
            Assert.IsNull(map.ObjectAt(snapshot, tent.Position), "the opened tent is gone");
        }

        [Test]
        public void FreedMaiden_GivesHerChainAndReachesHerGoal()
        {
            MapObjectDefinition maiden = First(MapObjectKind.Prison);
            ObjectiveDefinition goal = catalog.Mission.Objectives.First(objective => objective.TargetId == maiden.Id);
            CampaignState state = At(maiden.Position);
            Assert.IsFalse(objects.IsReached(state, goal));

            objects.Visit(state, maiden, out VisitOutcome freed);
            Assert.AreEqual(maiden.ArtefactId, freed.ArtefactId);
            Assert.AreEqual(ArtefactRarity.Epic, catalog.Artefact(maiden.ArtefactId).Rarity, "she gives an epic artefact");
            Assert.IsTrue(objects.IsReached(state, goal));
        }

        [Test]
        public void Goals_FiveWithTheBlackBannerRequiredToFinish()
        {
            CampaignState state = rules.NewCampaign();
            Assert.AreEqual(5, catalog.Mission.Objectives.Count);
            Assert.AreEqual(1, catalog.Mission.Objectives.Count(objective => objective.IsRequired));
            Assert.AreEqual(ObjectiveKind.DefeatEncounter, catalog.Mission.Objectives[0].Kind);
            Assert.AreEqual(EncounterKind.Boss, catalog.Mission.FindEncounter(catalog.Mission.Objectives[0].TargetId).Kind);
            Assert.IsFalse(objects.CanFinish(state));

            EncounterDefinition boss = catalog.Mission.Encounter(EncounterKind.Boss);
            rules.ApplyVictory(state, boss, state.Army, new VictoryRewards(1, new string[0], false));
            Assert.IsTrue(objects.CanFinish(state), "the other goals are optional");
            Assert.AreEqual(1, objects.ReachedCount(state));
        }

        [Test]
        public void GuardedTreasure_IsAlwaysTheVictorysArtefact()
        {
            EncounterDefinition guards = catalog.Mission.Encounters.First(encounter => encounter.RewardArtefactId != null);
            VictoryRewards rewards = rules.RollRewards(rules.NewCampaign(), guards, new FixedRandomSource(0.99), 0);
            CollectionAssert.AreEqual(new[] { guards.RewardArtefactId }, rewards.ArtefactIds);
        }

        [Test]
        public void Snapshot_RoundTripsObjectsKeysMoraleAndEquipment()
        {
            MapObjectDefinition horns = First(MapObjectKind.MoraleShrine);
            MapObjectDefinition skald = First(MapObjectKind.QuestGiver);
            CampaignState state = At(skald.Position);
            objects.Visit(state, skald, out _);
            CampaignState blessed = rules.Restore(Snapshot(state, horns.Position));
            objects.Visit(blessed, horns, out _);
            rules.DebugArtefacts(blessed);
            rules.Equip(blessed, blessed.Inventory.ToList().IndexOf("gloves-of-metal"));

            CampaignState restored = rules.Restore(blessed.Snapshot());
            Assert.AreEqual(JsonUtility.ToJson(blessed.Snapshot()), JsonUtility.ToJson(restored.Snapshot()));
            Assert.AreEqual("gloves-of-metal", restored.Equipped(EquipmentPlace.Gloves));
            Assert.IsTrue(restored.HasKey(skald.KeyId));
            Assert.AreEqual(rules.Morale(blessed), rules.Morale(restored));
            Assert.Greater(rules.Morale(restored), 0);
        }

        [Test]
        public void Rings_FillTheLeftHandThenTheRight()
        {
            CampaignState state = rules.NewCampaign();
            rules.DebugArtefacts(state);
            string[] rings = catalog.Artefacts.Where(artefact => artefact.Slot == EquipmentSlot.Ring).Select(artefact => artefact.Id).Take(2).ToArray();
            rules.Equip(state, state.Inventory.ToList().IndexOf(rings[0]));
            rules.Equip(state, state.Inventory.ToList().IndexOf(rings[1]));
            Assert.AreEqual(rings[0], state.Equipped(EquipmentPlace.LeftRing));
            Assert.AreEqual(rings[1], state.Equipped(EquipmentPlace.RightRing));
            int helm = state.Inventory.ToList().FindIndex(id => catalog.Artefact(id).Slot == EquipmentSlot.Head);
            string before = JsonUtility.ToJson(state.Snapshot());
            Assert.IsFalse(rules.EquipAt(state, helm, EquipmentPlace.LeftRing).IsSuccess, "an artefact is worn only where it fits");
            Assert.AreEqual(before, JsonUtility.ToJson(state.Snapshot()));
        }

        [Test]
        public void Morale_GoodMoraleCanGiveAnExtraAction()
        {
            CampaignState state = rules.NewCampaign();
            rules.DebugArtefacts(state);
            foreach (string id in new[] { "studded-belt", "war-crown", "ring-of-the-horned-god" })
            {
                rules.Equip(state, state.Inventory.ToList().IndexOf(id));
            }

            Assert.AreEqual(3, rules.Morale(state));
            EncounterDefinition bandits = catalog.Mission.Encounters.First(encounter => encounter.IsHostile);
            BattleEngine battle = new BattleEngine(catalog, new FixedRandomSource(0), state, bandits);
            while (!battle.Active.IsPlayer)
            {
                battle.Execute(new BattleAction(BattleActionKind.Pass), false);
            }

            battle.TakeMoraleEvents();
            Combatant actor = battle.Active;
            HexPosition step = battle.ReachableCells().First(cell => cell != actor.Position);
            battle.Execute(new BattleAction(BattleActionKind.MoveOrAttack, step), true);
            Assert.AreSame(actor, battle.Active, "a lucky stack acts again");
            Assert.IsTrue(battle.TakeMoraleEvents().Single().IsGood);
        }

        private MapObjectDefinition First(MapObjectKind kind)
        {
            return catalog.Mission.Objects.First(mapObject => mapObject.Kind == kind);
        }

        private CampaignState At(WorldPosition position)
        {
            return rules.Restore(Snapshot(rules.NewCampaign(), position));
        }

        private static CampaignSnapshot Snapshot(CampaignState state, WorldPosition position)
        {
            CampaignSnapshot snapshot = state.Snapshot();
            snapshot.x = position.X;
            snapshot.y = position.Y;
            return snapshot;
        }
    }
}
