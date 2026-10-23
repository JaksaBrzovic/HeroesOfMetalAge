using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The adventure map in the world: the painted map image, the hidden terrain logic layer, the hero, encounter and town
    /// markers, the planned route and the fog of war over unexplored land. Map cells are one world unit; rows of <see cref="WorldPosition"/> count from the top
    /// while grid rows count from the bottom, which is converted only here.
    /// </summary>
    public sealed class AdventureMapView : MonoBehaviour
    {
        private const int MAX_GLIDE_CELLS = 6;
        private const float ROUTE_STEP_SIZE = 0.22f;
        private const float ROUTE_END_SIZE = 0.42f;
        private const float MARKER_BASE_BLEND = 0.45f;
        // buildings may be wider than tall
        private const float OBJECT_WIDTH_FACTOR = 1.6f;

        [SerializeField] private Grid grid;
        [SerializeField] private SpriteRenderer background;
        [Tooltip("Renderer of the terrain logic layer; hidden in the game, shown with the development panel's TERRAIN OVERLAY.")]
        [SerializeField] private TilemapRenderer terrainOverlay;
        [Tooltip("Size of one cell on screen at the start, in UI design pixels (the 1920x1080 frame). 0 shows the image at its own pixel size.")]
        [Min(0f)]
        [SerializeField] private float displayPixelsPerCell;
        [Tooltip("Smallest and largest cell on screen (design pixels) the mouse wheel zooms between.")]
        [SerializeField] private Vector2 zoomPixelsPerCell = new Vector2(32f, 64f);
        [Tooltip("Off when the town is painted on the map image; its cell stays clickable.")]
        [SerializeField] private bool showTownMarker = true;

        [Header("Content")]
        [SerializeField] private Transform route;
        [SerializeField] private Transform heroes;
        [SerializeField] private Transform towns;
        [SerializeField] private Transform friendlyUnits;
        [SerializeField] private Transform enemies;
        [Tooltip("Parent of the map objects: pickups, chests, shrines, mills, dwellings, quest givers, gates, captives and portals.")]
        [SerializeField] private Transform mapObjects;
        [SerializeField] private WorldMarker markerPrefab;
        [SerializeField] private SpriteRenderer routeStepPrefab;
        [SerializeField] private SpriteRenderer hoverCell;
        [Tooltip("Fog of war over the unexplored cells; sorted above the markers. Its sprite is drawn at run time.")]
        [SerializeField] private SpriteRenderer fogOfWar;

        private readonly Dictionary<string, WorldMarker> encounterMarkers = new Dictionary<string, WorldMarker>();
        private readonly Dictionary<string, WorldMarker> objectMarkers = new Dictionary<string, WorldMarker>();
        private readonly List<SpriteRenderer> routeSteps = new List<SpriteRenderer>();
        private CanvasViewContext context;
        private WorldMarker heroMarker;
        private FogOfWarLayer fogLayer;

        public WorldMarker HeroMarker => heroMarker;
        public bool IsTerrainOverlayVisible => terrainOverlay.enabled;
        /// <summary>False once the whole map is explored.</summary>
        public bool IsFogOfWarVisible => fogOfWar.enabled;

        /// <summary>The map area in this object's space, one unit per cell.</summary>
        public Rect Bounds => new Rect(0f, 0f, World.Width, World.Height);

        /// <summary>Cell size on screen in UI design pixels; the map image's own pixels per cell unless overridden.</summary>
        public float DisplayPixelsPerCell => displayPixelsPerCell > 0f ? displayPixelsPerCell : background.sprite.pixelsPerUnit;
        public float MinimumPixelsPerCell => Mathf.Min(zoomPixelsPerCell.x, DisplayPixelsPerCell);
        public float MaximumPixelsPerCell => Mathf.Max(zoomPixelsPerCell.y, DisplayPixelsPerCell);

        private GameSession Session => context.Session;
        private WorldMap World => context.Session.World;

        /// <exception cref="InvalidOperationException">A reference is missing on the map prefab.</exception>
        public void Initialize(CanvasViewContext viewContext)
        {
            if (grid == null || background == null || background.sprite == null || terrainOverlay == null || route == null || heroes == null
                || towns == null || friendlyUnits == null || enemies == null || mapObjects == null || markerPrefab == null || routeStepPrefab == null
                || hoverCell == null || fogOfWar == null)
            {
                throw new InvalidOperationException($"AdventureMapView on '{name}' has unassigned references. Assign them in the map prefab.");
            }

            context = viewContext;
            terrainOverlay.enabled = false;
            hoverCell.gameObject.SetActive(false);
            // the fog's sprite starts at the map's bottom-left corner, one world unit per cell like the grid
            fogOfWar.transform.localPosition = transform.InverseTransformPoint(grid.CellToWorld(Vector3Int.zero));
            fogLayer = new FogOfWarLayer(fogOfWar, World.Width, World.Height);
            heroMarker = Instantiate(markerPrefab, heroes);
            FigureAnimation ride = context.Media.HeroAnimation(Session.Campaign.HeroId);
            heroMarker.SetFrames(ride.Frames, ride.FramesPerSecond);
            heroMarker.SetFacing(false, true);
            heroMarker.Place(CellCenter(Session.Campaign.Position));
            foreach (EncounterDefinition encounter in Session.Catalog.Mission.Encounters)
            {
                WorldMarker marker = Instantiate(markerPrefab, MarkerParent(encounter.Kind));
                marker.name = encounter.Name;
                marker.Place(CellCenter(encounter.Position));
                // armies wait facing the road from the west, where the hero comes from
                marker.SetFacing(true, false);
                encounterMarkers.Add(encounter.Id, marker);
            }

            foreach (MapObjectDefinition mapObject in Session.Catalog.Mission.Objects)
            {
                WorldMarker marker = Instantiate(markerPrefab, mapObjects);
                marker.name = mapObject.Name;
                marker.Place(CellCenter(mapObject.Position));
                marker.SetFacing(false, false);
                MapFigure figure = context.Media.ObjectFigure(mapObject.Id);
                marker.SetSize(figure.Height, figure.Height * OBJECT_WIDTH_FACTOR);
                marker.SetBaseVisible(false);
                marker.Display(figure.Sprite != null ? figure.Sprite : context.Theme.MapObject, Color.white, Color.clear, string.Empty);
                objectMarkers.Add(mapObject.Id, marker);
            }

            Refresh();
        }

        public WorldMarker ObjectMarker(string objectId)
        {
            return objectMarkers[objectId];
        }

        public WorldMarker EncounterMarker(string encounterId)
        {
            return encounterMarkers[encounterId];
        }

        public Vector3 CellCenter(WorldPosition position)
        {
            return transform.InverseTransformPoint(grid.GetCellCenterWorld(GridCell(position)));
        }

        /// <summary>Centre of a cell in world space.</summary>
        public Vector3 CellCenterWorld(WorldPosition position)
        {
            return grid.GetCellCenterWorld(GridCell(position));
        }

        public bool TryGetCell(Vector3 worldPoint, out WorldPosition position)
        {
            Vector3Int cell = grid.WorldToCell(worldPoint);
            position = new WorldPosition(cell.x, World.Height - 1 - cell.y);
            return World.IsInside(position);
        }

        public void Refresh()
        {
            RefreshEncounters();
            RefreshObjects();
            RefreshHero();
            RefreshRoute();
            fogLayer.Show(Session.Campaign.Explored);
        }

        public void Tick(float deltaTime)
        {
            heroMarker.Tick(deltaTime);
        }

        public void ShowHover(WorldPosition position)
        {
            // unexplored land gives nothing away, not even whether it can be crossed
            if (!Session.Campaign.Explored.IsExplored(position))
            {
                HideHover();
                return;
            }

            hoverCell.gameObject.SetActive(true);
            hoverCell.transform.localPosition = CellCenter(position);
            hoverCell.color = World.IsWalkable(position) ? context.Theme.Gold : context.Theme.Enemy;
        }

        public void HideHover()
        {
            hoverCell.gameObject.SetActive(false);
        }

        public void SetTerrainOverlayVisible(bool isVisible)
        {
            terrainOverlay.enabled = isVisible;
        }

        private void OnDestroy()
        {
            fogLayer?.Dispose();
        }

        private Vector3Int GridCell(WorldPosition position)
        {
            return new Vector3Int(position.X, World.Height - 1 - position.Y, 0);
        }

        private Transform MarkerParent(EncounterKind kind)
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

        private void RefreshEncounters()
        {
            CampaignState campaign = Session.Campaign;
            foreach (EncounterDefinition encounter in Session.Catalog.Mission.Encounters)
            {
                WorldMarker marker = encounterMarkers[encounter.Id];
                bool isVisible = encounter.Kind == EncounterKind.Town ? showTownMarker : !campaign.IsCompleted(encounter.Id);
                UiBinding.SetVisible(marker.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                if (encounter.Kind == EncounterKind.Town)
                {
                    marker.Display(context.TownIcon, context.TownTint, BaseColor(context.Theme.Gold), string.Empty);
                    continue;
                }

                Color side = encounter.Kind == EncounterKind.Friendly ? context.Theme.Friendly : context.Theme.Enemy;
                string leaderId = encounter.Army[0].UnitId;
                Sprite figure = context.Media.EncounterFigure(encounter.Id);
                // authored art keeps its colours; placeholder icons show the side
                Color tint = figure != null || context.Media.Unit(leaderId) != null ? Color.white : side;
                marker.Display(figure != null ? figure : context.UnitIcon(leaderId), tint, BaseColor(side), ArmySize(encounter.Army).ToString());
            }
        }

        // taken pickups, opened chests and gates and freed captives leave the map
        private void RefreshObjects()
        {
            CampaignState campaign = Session.Campaign;
            foreach (MapObjectDefinition mapObject in Session.Catalog.Mission.Objects)
            {
                bool isThere = !(mapObject.IsRemovedWhenVisited && campaign.IsVisited(mapObject.Id));
                UiBinding.SetVisible(objectMarkers[mapObject.Id].gameObject, isThere);
            }
        }

        // a short walk glides cell by cell, one cell per travel step so the ride does not halt between cells; a jump (load,
        // new mission) is shown at once
        private void RefreshHero()
        {
            HeroDefinition hero = Session.Catalog.Hero(Session.Campaign.HeroId);
            heroMarker.name = hero.Name;
            heroMarker.Display(context.HeroIcon, context.HeroTint, BaseColor(context.Theme.Gold), string.Empty);
            Vector3 target = CellCenter(Session.Campaign.Position);
            float distance = Vector3.Distance(heroMarker.Target, target);
            if (distance > MAX_GLIDE_CELLS)
            {
                heroMarker.Place(target);
            }
            else if (distance > 0f)
            {
                heroMarker.MoveTo(target, distance / Mathf.Max(Session.TravelStepSeconds, 0.01f));
            }
        }

        private void RefreshRoute()
        {
            IReadOnlyList<WorldPosition> steps = Session.Route;
            while (routeSteps.Count < steps.Count)
            {
                routeSteps.Add(Instantiate(routeStepPrefab, route));
            }

            int cost = 0;
            WorldPosition previous = Session.Campaign.Position;
            for (int i = 0; i < routeSteps.Count; i++)
            {
                SpriteRenderer step = routeSteps[i];
                bool isVisible = i < steps.Count;
                UiBinding.SetVisible(step.gameObject, isVisible);
                if (!isVisible)
                {
                    continue;
                }

                cost += World.StepCost(previous, steps[i]);
                previous = steps[i];
                step.transform.localPosition = CellCenter(steps[i]);
                step.transform.localScale = Vector3.one * (i == steps.Count - 1 ? ROUTE_END_SIZE : ROUTE_STEP_SIZE);
                // the part of the route that today's movement cannot pay for is shown in the enemy colour
                step.color = cost <= Session.Campaign.Movement ? context.Theme.Gold : context.Theme.Enemy;
            }
        }

        private Color BaseColor(Color side)
        {
            Color color = Color.Lerp(context.Theme.Panel, side, MARKER_BASE_BLEND);
            color.a = 0.92f;
            return color;
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
    }
}
