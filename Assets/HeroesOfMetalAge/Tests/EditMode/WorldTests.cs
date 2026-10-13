using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Bootstrap;
using HeroesOfMetalAge.Domain;
using HeroesOfMetalAge.EditorTools;
using HeroesOfMetalAge.Infrastructure.Content;
using HeroesOfMetalAge.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace HeroesOfMetalAge.Tests
{
    /// <summary>The adventure map and battlefield drawn in the world: prefabs, the logic layer, cell and hex picking, routing scale.</summary>
    public class WorldTests
    {
        private const int LARGE_MAP_CELLS = 256;
        private const int STRAIGHT_MAP_CELLS = 24;
        private const long LARGE_ROUTE_BUDGET_MILLISECONDS = 2000;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.DestroyImmediate(item);
            }

            created.Clear();
        }

        [Test]
        public void WorldPrefab_HasCamerasLightMapsAndBattlefield()
        {
            GameWorld world = ContentAssets.WorldPrefab;
            Assert.IsNotNull(world);
            Assert.IsNotNull(world.Maps);
            Assert.IsTrue(world.Camera.Camera.orthographic);
            Assert.AreEqual(2, world.GetComponentsInChildren<Camera>(true).Length, "background and world cameras");
            Assert.AreEqual(1, world.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.IsTrue(world.GetComponentsInChildren<Component>(true).Any(component => component.GetType().Name == "Light2D"), "global 2D light");

            Transform battlefield = world.Battlefield.transform;
            foreach (string group in new[] { "Background", "Hexes", "Units/Player Units", "Units/Enemy Units", "Effects" })
            {
                Assert.IsNotNull(battlefield.Find(group), group);
            }

            Assert.IsNotNull(PrefabUtility.GetCorrespondingObjectFromSource(world.Battlefield), "the battlefield stays a nested prefab");
        }

        [Test]
        public void MissionMap_IsBuiltAndGroupsItsContent()
        {
            AdventureMapAuthoring map = ContentAssets.Mission.Map;
            Assert.IsNotNull(map);
            Assert.IsTrue(map.IsBuilt, "rebuilt for its image and cell size");
            Assert.IsNotNull(map.GetComponent<AdventureMapView>());
            foreach (string group in new[] { "Background", "Grid/Terrain Logic", "Route", "Towns", "Friendly Units", "Enemies", "Heroes" })
            {
                Assert.IsNotNull(map.transform.Find(group), group);
            }
        }

        [Test]
        public void LogicLayer_IsReadFromTheTopLeftAndUnpaintedCellsAreBlocked()
        {
            AdventureMapAuthoring map = NewMap(out Tilemap logic);
            map.ApplyBuild(64, 3, 2);
            logic.SetTile(new Vector3Int(0, 1, 0), Tile(TerrainType.Forest));
            logic.SetTile(new Vector3Int(2, 0, 0), Tile(TerrainType.Water));

            TerrainType[] terrain = map.ReadTerrain();

            Assert.AreEqual(TerrainType.Forest, terrain[0], "top-left cell is the first");
            Assert.AreEqual(TerrainType.Water, terrain[1 * 3 + 2], "bottom-right cell is the last");
            Assert.AreEqual(TerrainType.Blocked, terrain[1], "unpainted cells are impassable");
        }

        [Test]
        public void Rebuild_KeepsPaintedShapesWhenTheCellSizeChanges()
        {
            NewMap(out Tilemap logic);
            TerrainTile road = Tile(TerrainType.Road);
            TerrainTile water = Tile(TerrainType.Water);
            logic.SetTile(new Vector3Int(0, 0, 0), road);
            logic.SetTile(new Vector3Int(1, 1, 0), water);

            AdventureMapBuilder.Resample(logic, 64, 32, 4, 4);

            Assert.AreSame(road, logic.GetTile(new Vector3Int(1, 1, 0)), "one old cell becomes four smaller ones");
            Assert.AreSame(water, logic.GetTile(new Vector3Int(2, 3, 0)));
            Assert.IsNull(logic.GetTile(new Vector3Int(3, 0, 0)));

            AdventureMapBuilder.Crop(logic, 2, 2);
            Assert.IsNull(logic.GetTile(new Vector3Int(2, 3, 0)), "cells outside a smaller map are removed");
        }

        [Test]
        public void MapCells_RoundTripThroughWorldSpace()
        {
            GameSession session = NewSession();
            AdventureMapView map = Track((GameObject)PrefabUtility.InstantiatePrefab(ContentAssets.Mission.Map.gameObject)).GetComponent<AdventureMapView>();
            map.Initialize(Context(session));

            for (int y = 0; y < session.World.Height; y++)
            {
                for (int x = 0; x < session.World.Width; x++)
                {
                    WorldPosition cell = new WorldPosition(x, y);
                    Assert.IsTrue(map.TryGetCell(map.CellCenterWorld(cell), out WorldPosition picked) && picked == cell, x + "," + y);
                }
            }

            Assert.IsFalse(map.TryGetCell(new Vector3(-0.5f, -0.5f, 0f), out _), "outside the map");
            Assert.IsTrue(map.HeroMarker.HasVisibleIcon);
            Assert.IsTrue(session.Catalog.Mission.Encounters.Where(encounter => encounter.Kind != EncounterKind.Town).All(encounter => map.EncounterMarker(encounter.Id).HasVisibleIcon));
        }

        [Test]
        public void BattlefieldHexes_RoundTripThroughWorldSpace()
        {
            GameSession session = NewSession();
            BattlefieldView battlefield = Track((GameObject)PrefabUtility.InstantiatePrefab(ContentAssets.BattlefieldPrefab.gameObject))
                .GetComponent<BattlefieldView>();
            battlefield.Initialize(Context(session));
            CombatPolicy policy = session.Catalog.Mission.Combat;

            for (int y = 0; y < policy.Height; y++)
            {
                for (int x = 0; x < policy.Width; x++)
                {
                    HexPosition hex = new HexPosition(x, y);
                    Assert.IsTrue(battlefield.TryGetHex(battlefield.HexCenterWorld(hex), out HexPosition picked) && picked.Equals(hex), x + "," + y);
                }
            }

            Assert.IsFalse(battlefield.TryGetHex(battlefield.FrameBounds.max + Vector2.one, out _), "outside the board");
            Assert.AreEqual(policy.Width * policy.Height, battlefield.transform.Find("Hexes").childCount);
        }

        [Test]
        public void Routing_StaysFastOnLargeMaps()
        {
            WorldMap world = OpenWorld(LARGE_MAP_CELLS, new WorldPosition(0, 0), out CampaignState campaign);
            WorldPosition corner = new WorldPosition(LARGE_MAP_CELLS - 1, LARGE_MAP_CELLS - 1);

            Stopwatch timer = Stopwatch.StartNew();
            IReadOnlyList<WorldPosition> route = world.FindRoute(campaign, corner);
            timer.Stop();

            Assert.AreEqual(corner, route.Last());
            Assert.Less(timer.ElapsedMilliseconds, LARGE_ROUTE_BUDGET_MILLISECONDS);
        }

        [Test]
        public void TerrainRules_PriceTerrainAndDiagonalsLikeClassicStrategyGames()
        {
            TerrainRules terrain = ContentAssets.LoadCatalog().Mission.TerrainRules;

            Assert.IsFalse(terrain.IsPassable(TerrainType.Forest), "trees stop a mounted hero");
            Assert.IsFalse(terrain.IsPassable(TerrainType.Mountain));
            Assert.IsFalse(terrain.IsPassable(TerrainType.Water));
            Assert.IsFalse(terrain.IsPassable(TerrainType.Lava));
            Assert.IsFalse(terrain.IsPassable(TerrainType.Obstacle));
            Assert.IsFalse(terrain.IsPassable(TerrainType.Blocked));
            Assert.Less(terrain.Cost(TerrainType.Road), terrain.Cost(TerrainType.Grass), "roads are faster");
            Assert.Less(terrain.Cost(TerrainType.Grass), terrain.Cost(TerrainType.Sand));
            Assert.Less(terrain.Cost(TerrainType.Sand), terrain.Cost(TerrainType.Swamp));
            Assert.AreEqual(terrain.Cost(TerrainType.Grass) * 141 / 100, terrain.StepCost(TerrainType.Grass, true), "diagonals cost √2");
        }

        [Test]
        public void EquallyCheapRoutes_PreferTheStraightLine()
        {
            // rows below the mission's own encounters, so no site is in the way
            WorldMap world = OpenWorld(STRAIGHT_MAP_CELLS, new WorldPosition(2, 15), out CampaignState campaign);
            Assert.IsTrue(world.FindRoute(campaign, new WorldPosition(9, 15)).All(step => step.Y == 15), "horizontal");

            world = OpenWorld(STRAIGHT_MAP_CELLS, new WorldPosition(15, 12), out campaign);
            Assert.IsTrue(world.FindRoute(campaign, new WorldPosition(15, 20)).All(step => step.X == 15), "vertical");

            world = OpenWorld(STRAIGHT_MAP_CELLS, new WorldPosition(2, 12), out campaign);
            IReadOnlyList<WorldPosition> diagonal = world.FindRoute(campaign, new WorldPosition(8, 18));
            Assert.AreEqual(6, diagonal.Count, "a pure diagonal needs no straight steps");
            Assert.IsTrue(diagonal.All(step => step.X - 2 == step.Y - 12), "diagonal");
        }

        /// <summary>An open grass map of the given size with the real mission's content, the hero starting at <paramref name="start"/>.</summary>
        private static WorldMap OpenWorld(int size, WorldPosition start, out CampaignState campaign)
        {
            DefinitionCatalog real = ContentAssets.LoadCatalog();
            MissionDefinition mission = real.Mission;
            TerrainType[] open = Enumerable.Repeat(TerrainType.Grass, size * size).ToArray();
            MissionDefinition openMission = new MissionDefinition(mission.Id, mission.Name, mission.Description, mission.HeroId, mission.ClanId,
                mission.ClanName, mission.ClanHeroName, mission.StartingGold, mission.ArmySlots, mission.DaysPerWeek, mission.TerrainRules,
                mission.OwnedPriceMultiplier, size, size, start, open, mission.Combat, mission.Encounters, mission.RecruitmentIds);
            DefinitionCatalog catalog = new DefinitionCatalog(real.Units, real.Artefacts, real.Heroes, openMission);
            campaign = new CampaignRules(catalog).NewCampaign();
            return new WorldMap(openMission);
        }

        private AdventureMapAuthoring NewMap(out Tilemap logic)
        {
            GameObject root = Track(new GameObject("map"));
            GameObject layer = new GameObject("logic");
            layer.transform.SetParent(new GameObject("grid", typeof(Grid)).transform);
            layer.transform.parent.SetParent(root.transform);
            logic = layer.AddComponent<Tilemap>();
            AdventureMapAuthoring map = root.AddComponent<AdventureMapAuthoring>();
            map.Configure(null, null, 64, logic);
            return map;
        }

        private TerrainTile Tile(TerrainType terrain)
        {
            TerrainTile tile = ScriptableObject.CreateInstance<TerrainTile>();
            tile.Terrain = terrain;
            created.Add(tile);
            return tile;
        }

        private GameObject Track(GameObject item)
        {
            created.Add(item);
            return item;
        }

        private static GameSession NewSession()
        {
            GameSession session = CampaignTestUtility.NewSession(ContentAssets.LoadCatalog(), new MemorySaveStore());
            session.StartMission();
            return session;
        }

        private static CanvasViewContext Context(GameSession session)
        {
            PresentationAssets media = GameBootstrap.CreatePresentationAssets(ContentAssets.Units, ContentAssets.Artefacts, ContentAssets.Heroes,
                ContentAssets.Mission);
            return new CanvasViewContext(session, new FakeAudioControls(), media, ContentAssets.Theme);
        }
    }
}
