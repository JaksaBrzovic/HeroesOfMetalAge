using System;
using System.Linq;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using NUnit.Framework;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>Fog of war: what the hero has seen, how riding explores, routes that stay out of the fog and saving it.</summary>
    public class FogOfWarTests
    {
        private DefinitionCatalog catalog;
        private CampaignRules rules;
        private WorldMap world;

        [SetUp]
        public void SetUp()
        {
            catalog = ContentAssets.LoadCatalog();
            rules = new CampaignRules(catalog);
            world = new WorldMap(catalog.Mission);
        }

        [Test]
        public void NewCampaign_ExploresOnlyAroundTheHero()
        {
            CampaignState state = rules.NewCampaign();
            int radius = catalog.Mission.ScoutingRadius;
            WorldPosition start = state.Position;

            Assume.That(radius > 0, "the mission uses the fog of war");
            Assert.IsTrue(state.Explored.IsExplored(start));
            Assert.IsTrue(state.Explored.IsExplored(Clamp(start.X + radius, start.Y)), "the edge of sight along a row");
            Assert.IsFalse(state.Explored.IsExplored(new WorldPosition(start.X + radius + 1, start.Y)), "just beyond sight");
            Assert.IsFalse(state.Explored.IsExplored(catalog.Mission.Town.Position), "the town lies far away in the fog");
        }

        [Test]
        public void Routes_NeverLeadIntoTheFog()
        {
            CampaignState state = rules.NewCampaign();
            WorldPosition town = catalog.Mission.Town.Position;
            WorldPosition sentinel = catalog.Mission.Encounter(EncounterKind.Sentinel).Position;

            Assert.AreEqual(0, world.FindRoute(state, town).Count, "an unexplored destination");
            Assume.That(!state.Explored.IsExplored(sentinel));
            Assert.AreEqual(0, world.FindRoute(state, sentinel).Count);

            rules.DebugRevealMap(state);
            Assert.IsTrue(state.Explored.IsComplete);
            Assert.Greater(world.FindRoute(state, sentinel).Count, 0, "the same ride once the map is known");
        }

        [Test]
        public void Riding_ExploresAheadAndNeverForgets()
        {
            CampaignState state = rules.NewCampaign();
            WorldPosition start = state.Position;
            int explored = state.Explored.Count;
            WorldPosition step = Neighbours(start).First(cell => world.CanStep(start, cell) && world.Site(state, cell) == null
                && world.ObjectAt(state, cell) == null);

            Assert.IsTrue(rules.Move(state, step, world).IsSuccess);
            Assert.Greater(state.Explored.Count, explored, "a step reveals new land");
            int afterStep = state.Explored.Count;
            Assert.IsTrue(rules.Move(state, start, world).IsSuccess);
            Assert.AreEqual(afterStep, state.Explored.Count, "riding back neither reveals nor forgets anything");
        }

        [Test]
        public void ExploredLand_SurvivesSaveAndLoad()
        {
            CampaignState state = rules.NewCampaign();
            rules.DebugRevealMap(state);
            CampaignState loaded = rules.Restore(state.Snapshot());
            Assert.IsTrue(loaded.Explored.IsComplete);

            CampaignState fresh = rules.NewCampaign();
            CampaignState reloaded = rules.Restore(fresh.Snapshot());
            Assert.AreEqual(fresh.Explored.Count, reloaded.Explored.Count);
            Assert.AreEqual(fresh.Snapshot().explored, reloaded.Snapshot().explored, "the same cells");
        }

        [Test]
        public void SaveWithoutExploredLand_LoadsWithTheHerosSurroundings()
        {
            CampaignState state = rules.NewCampaign();
            CampaignSnapshot snapshot = state.Snapshot();
            snapshot.explored = null;

            CampaignState loaded = rules.Restore(snapshot);
            Assert.AreEqual(state.Explored.Count, loaded.Explored.Count, "a save made before the fog of war");
        }

        [Test]
        public void DamagedExploredLand_IsRejected()
        {
            CampaignSnapshot snapshot = rules.NewCampaign().Snapshot();
            snapshot.explored = Convert.ToBase64String(new byte[3]);
            Assert.Throws<ArgumentException>(() => rules.Restore(snapshot), "wrong size");

            snapshot.explored = "not base64!";
            Assert.Throws<ArgumentException>(() => rules.Restore(snapshot), "not Base64");
        }

        private WorldPosition Clamp(int x, int y)
        {
            return new WorldPosition(Math.Min(x, catalog.Mission.Width - 1), y);
        }

        private static WorldPosition[] Neighbours(WorldPosition cell)
        {
            return new[]
            {
                new WorldPosition(cell.X + 1, cell.Y), new WorldPosition(cell.X, cell.Y + 1), new WorldPosition(cell.X, cell.Y - 1),
                new WorldPosition(cell.X - 1, cell.Y)
            };
        }
    }
}
