using System;
using System.Collections.Generic;
using System.Text;
using HeroesOfMetalAge.Domain;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>Parent object for one terrain type's tiles in the Adventure prefab.</summary>
    [Serializable]
    public sealed class TerrainContainer
    {
        [SerializeField] private TerrainType type;
        [SerializeField] private RectTransform root;

        public TerrainType Type => type;
        public RectTransform Root => root;
    }

    /// <summary>A tile saved in the prefab for a map cell, so the starting map is visible in Prefab Mode.</summary>
    [Serializable]
    public sealed class WorldTileBinding
    {
        [SerializeField] private Vector2Int position;
        [SerializeField] private UiWidget widget;

        public Vector2Int Position => position;
        public UiWidget Widget => widget;
    }

    /// <summary>An encounter marker saved in the prefab, matched to the mission by encounter ID.</summary>
    [Serializable]
    public sealed class WorldEncounterBinding
    {
        [SerializeField] private string encounterId;
        [SerializeField] private UiWidget widget;

        public string EncounterId => encounterId;
        public UiWidget Widget => widget;
    }

    /// <summary>
    /// The adventure screen: terrain tiles, encounter markers, the hero, the planned route and the hero panel. Clicking a
    /// cell asks the session to plan (or ride) there.
    /// </summary>
    public sealed class WorldCanvasView : CanvasScreenView
    {
        private const float ROUTE_THICKNESS = 13f;

        [Header("Hero panel")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text heroText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text movementText;
        [SerializeField] private Image movementBarImage;
        [SerializeField] private TMP_Text objectivesText;
        [SerializeField] private UiWidget rideButton;
        [SerializeField] private UiWidget endDayButton;
        [SerializeField] private ArmySlotsView armySlots;

        [Header("Map")]
        [SerializeField] private GridLayoutGroup layout;
        [SerializeField] private UiWidget tilePrefab;
        [SerializeField] private TerrainContainer[] terrainContainers;
        [SerializeField] private WorldTileBinding[] authoredTiles;
        [SerializeField] private RectTransform routeLayer;
        [SerializeField] private Image routePrefab;

        [Header("Markers")]
        [SerializeField] private UiWidget heroMarker;
        [SerializeField] private UiWidget encounterMarkerPrefab;
        [SerializeField] private WorldEncounterBinding[] authoredEncounters;
        [SerializeField] private RectTransform heroes;
        [SerializeField] private RectTransform enemies;
        [SerializeField] private RectTransform friendlyUnits;
        [SerializeField] private RectTransform towns;

        private readonly Dictionary<TerrainType, RectTransform> terrainRoots = new Dictionary<TerrainType, RectTransform>();
        private readonly Dictionary<string, UiWidget> encounterMarkers = new Dictionary<string, UiWidget>();
        private readonly List<Image> routeSegments = new List<Image>();
        private readonly StringBuilder objectives = new StringBuilder();
        private UiWidget[,] tiles;

        public UiWidget HeroMarker => heroMarker;

        public UiWidget EncounterMarker(string encounterId)
        {
            return encounterMarkers[encounterId];
        }

        /// <summary>The clickable widget at a cell: the hero, an encounter marker or the terrain tile.</summary>
        public UiWidget Tile(WorldPosition position)
        {
            if (Campaign.Position == position)
            {
                return heroMarker;
            }

            EncounterDefinition site = Session.World.Site(Campaign, position);
            return site != null ? encounterMarkers[site.Id] : tiles[position.X, position.Y];
        }

        public override void Refresh()
        {
            HeroDefinition hero = Catalog.Hero(Campaign.HeroId);
            int dailyMovement = Rules.DailyMovement(Campaign);
            UiBinding.SetText(titleText, Catalog.Mission.Name);
            UiBinding.SetText(descriptionText, Catalog.Mission.Description);
            UiBinding.SetText(heroText, hero.Name);
            UiBinding.SetText(levelText, hero.ClassName + " / LEVEL " + Rules.Level(Campaign));
            UiBinding.SetSprite(portraitImage, Context.HeroIcon, Context.HeroTint);
            UiBinding.SetText(movementText, "MOVEMENT " + Campaign.Movement + " / " + dailyMovement);
            UiBinding.SetProgress(movementBarImage, Campaign.Movement / (float)dailyMovement);
            UiBinding.SetText(objectivesText, BuildObjectives());
            UiBinding.SetButton(rideButton, "RIDE [SPACE]", Session.Route.Count > 0 && !Session.IsBusy);
            UiBinding.SetButton(endDayButton, "END DAY [E]", !Session.IsBusy);
            armySlots.Display(Campaign.Army, true);

            RefreshEncounterMarkers();
            heroMarker.Rect.anchoredPosition = CellOrigin(Campaign.Position);
            heroMarker.name = hero.Name;
            heroMarker.Display(string.Empty, string.Empty, string.Empty, Context.HeroIcon, Context.HeroTint, !Session.IsBusy);
            RefreshRoute();
        }

        protected override void ValidateReferences()
        {
            Require(titleText, nameof(titleText));
            Require(descriptionText, nameof(descriptionText));
            Require(heroText, nameof(heroText));
            Require(levelText, nameof(levelText));
            Require(portraitImage, nameof(portraitImage));
            Require(movementText, nameof(movementText));
            Require(movementBarImage, nameof(movementBarImage));
            Require(objectivesText, nameof(objectivesText));
            Require(rideButton, nameof(rideButton));
            Require(endDayButton, nameof(endDayButton));
            Require(armySlots, nameof(armySlots));
            Require(layout, nameof(layout));
            Require(tilePrefab, nameof(tilePrefab));
            Require(routeLayer, nameof(routeLayer));
            Require(routePrefab, nameof(routePrefab));
            Require(heroMarker, nameof(heroMarker));
            Require(encounterMarkerPrefab, nameof(encounterMarkerPrefab));
            Require(heroes, nameof(heroes));
            Require(enemies, nameof(enemies));
            Require(friendlyUnits, nameof(friendlyUnits));
            Require(towns, nameof(towns));
        }

        protected override void BindControls()
        {
            rideButton.Bind(Session.Travel);
            endDayButton.Bind(Session.EndDay);
            armySlots.Initialize(Context);
            foreach (TerrainContainer container in terrainContainers)
            {
                terrainRoots.Add(container.Type, container.Root);
            }

            // cells are positioned explicitly; the layout group only provides cell size, spacing and padding
            layout.enabled = false;
            BindTiles();
            BindEncounterMarkers();
            heroMarker.Bind(Session.ShowHero);
        }

        private void BindTiles()
        {
            WorldMap world = Session.World;
            tiles = new UiWidget[world.Width, world.Height];
            foreach (WorldTileBinding binding in authoredTiles)
            {
                WorldPosition position = new WorldPosition(binding.Position.x, binding.Position.y);
                if (!world.IsInside(position))
                {
                    binding.Widget.gameObject.SetActive(false);
                    continue;
                }

                tiles[position.X, position.Y] = binding.Widget;
            }

            for (int y = 0; y < world.Height; y++)
            {
                for (int x = 0; x < world.Width; x++)
                {
                    BindTile(new WorldPosition(x, y));
                }
            }
        }

        private void BindTile(WorldPosition position)
        {
            TerrainType terrain = Session.World.Terrain(position);
            UiWidget tile = tiles[position.X, position.Y];
            if (tile == null)
            {
                tile = Instantiate(tilePrefab, terrainRoots[terrain]);
                tiles[position.X, position.Y] = tile;
            }

            tile.transform.SetParent(terrainRoots[terrain], false);
            tile.Rect.anchoredPosition = CellOrigin(position);
            tile.name = terrain + " Tile " + position.X + "," + position.Y;
            tile.Bind(() => Session.Plan(position));

            // terrain is fixed for the mission, so it is painted once here instead of on every refresh
            tile.SetBackground(Context.Theme.TerrainColor(terrain));
            tile.SetIcon(Context.Theme.TerrainIcon(terrain), Context.Theme.TerrainIconColor(terrain));
        }

        private void BindEncounterMarkers()
        {
            foreach (WorldEncounterBinding binding in authoredEncounters)
            {
                if (Catalog.Mission.HasEncounter(binding.EncounterId))
                {
                    encounterMarkers.Add(binding.EncounterId, binding.Widget);
                }
                else
                {
                    binding.Widget.gameObject.SetActive(false);
                }
            }

            foreach (EncounterDefinition encounter in Catalog.Mission.Encounters)
            {
                if (!encounterMarkers.TryGetValue(encounter.Id, out UiWidget marker))
                {
                    marker = Instantiate(encounterMarkerPrefab, MarkerParent(encounter.Kind));
                    encounterMarkers.Add(encounter.Id, marker);
                }

                WorldPosition position = encounter.Position;
                marker.Rect.anchoredPosition = CellOrigin(position);
                marker.Bind(() => Session.Plan(position));
            }
        }

        private RectTransform MarkerParent(EncounterKind kind)
        {
            switch (kind)
            {
                case EncounterKind.Town:
                    return towns;
                case EncounterKind.Friendly:
                    return friendlyUnits;
                default:
                    return enemies;
            }
        }

        private string BuildObjectives()
        {
            MissionDefinition mission = Catalog.Mission;
            objectives.Clear();
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Friendly).Id), "Gather the willing");
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Sentinel).Id), "Defeat the pass wardens");
            AppendObjective(Campaign.HasRecruited, "Recruit at the town");
            AppendObjective(Campaign.IsCompleted(mission.Encounter(EncounterKind.Boss).Id), "Break the Black Banner");
            return objectives.ToString().TrimEnd('\n');
        }

        private void AppendObjective(bool isDone, string text)
        {
            objectives.Append(isDone ? "[+] " : "[ ] ").Append(text).Append('\n');
        }

        private void RefreshEncounterMarkers()
        {
            foreach (EncounterDefinition encounter in Catalog.Mission.Encounters)
            {
                UiWidget marker = encounterMarkers[encounter.Id];
                bool isVisible = encounter.Kind == EncounterKind.Town || !Campaign.IsCompleted(encounter.Id);
                UiBinding.SetVisible(marker.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                if (encounter.Kind == EncounterKind.Town)
                {
                    marker.Display(string.Empty, string.Empty, string.Empty, Context.TownIcon, Context.TownTint, true);
                    continue;
                }

                Color tint = encounter.Kind == EncounterKind.Friendly ? Context.Theme.Friendly : Context.Theme.Enemy;
                marker.Display(string.Empty, string.Empty, ArmySize(encounter.Army).ToString(), Context.UnitIcon(encounter.Army[0].UnitId), tint, true);
            }
        }

        private void RefreshRoute()
        {
            while (routeSegments.Count < Session.Route.Count)
            {
                routeSegments.Add(Instantiate(routePrefab, routeLayer));
            }

            WorldPosition previous = Campaign.Position;
            int cost = 0;
            for (int i = 0; i < routeSegments.Count; i++)
            {
                Image segment = routeSegments[i];
                bool isVisible = i < Session.Route.Count;
                UiBinding.SetVisible(segment.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                WorldPosition step = Session.Route[i];
                cost += Session.World.Cost(step);
                PlaceSegment(segment.rectTransform, CellCenter(previous), CellCenter(step));
                // the part of the route that today's movement cannot pay for is shown in the enemy colour
                segment.color = cost <= Campaign.Movement ? Context.Theme.Gold : Context.Theme.Enemy;
                previous = step;
            }
        }

        private static void PlaceSegment(RectTransform segment, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            segment.anchoredPosition = (from + to) * 0.5f;
            segment.sizeDelta = new Vector2(delta.magnitude, ROUTE_THICKNESS);
            segment.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private static int ArmySize(IReadOnlyList<Troop> army)
        {
            int size = 0;
            foreach (Troop troop in army)
            {
                size += troop.Count;
            }

            return size;
        }

        private Vector2 CellOrigin(WorldPosition position)
        {
            Vector2 stride = layout.cellSize + layout.spacing;
            return new Vector2(layout.padding.left + position.X * stride.x, -layout.padding.top - position.Y * stride.y);
        }

        private Vector2 CellCenter(WorldPosition position)
        {
            return CellOrigin(position) + new Vector2(layout.cellSize.x * 0.5f, -layout.cellSize.y * 0.5f);
        }
    }
}
