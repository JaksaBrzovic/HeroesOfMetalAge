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
    /// markers and the planned route. Map cells are one world unit; rows of <see cref="WorldPosition"/> count from the top
    /// while grid rows count from the bottom, which is converted only here.
    /// </summary>
    public sealed class AdventureMapView : MonoBehaviour
    {
        private const int MAX_GLIDE_CELLS = 6;
        private const float ROUTE_STEP_SIZE = 0.22f;
        private const float ROUTE_END_SIZE = 0.42f;
        private const float MARKER_BASE_BLEND = 0.45f;

        [SerializeField] private Grid grid;
        [SerializeField] private SpriteRenderer background;
        [Tooltip("Renderer of the terrain logic layer; hidden in the game, shown with the development panel's TERRAIN OVERLAY.")]
        [SerializeField] private TilemapRenderer terrainOverlay;
        [Tooltip("Size of one cell on screen, in UI design pixels (the 1600x900 frame). 0 shows the image at its own pixel size.")]
        [Min(0f)]
        [SerializeField] private float displayPixelsPerCell;
        [Tooltip("Off when the town is painted on the map image; its cell stays clickable.")]
        [SerializeField] private bool showTownMarker = true;

        [Header("Content")]
        [SerializeField] private Transform route;
        [SerializeField] private Transform heroes;
        [SerializeField] private Transform towns;
        [SerializeField] private Transform friendlyUnits;
        [SerializeField] private Transform enemies;
        [SerializeField] private WorldMarker markerPrefab;
        [SerializeField] private SpriteRenderer routeStepPrefab;
        [SerializeField] private SpriteRenderer hoverCell;

        private readonly Dictionary<string, WorldMarker> encounterMarkers = new Dictionary<string, WorldMarker>();
        private readonly List<SpriteRenderer> routeSteps = new List<SpriteRenderer>();
        private CanvasViewContext context;
        private WorldMarker heroMarker;

        public WorldMarker HeroMarker => heroMarker;
        public bool IsTerrainOverlayVisible => terrainOverlay.enabled;

        /// <summary>The map area in this object's space, one unit per cell.</summary>
        public Rect Bounds => new Rect(0f, 0f, World.Width, World.Height);

        /// <summary>Cell size on screen in UI design pixels; the map image's own pixels per cell unless overridden.</summary>
        public float DisplayPixelsPerCell => displayPixelsPerCell > 0f ? displayPixelsPerCell : background.sprite.pixelsPerUnit;

        private GameSession Session => context.Session;
        private WorldMap World => context.Session.World;

        /// <exception cref="InvalidOperationException">A reference is missing on the map prefab.</exception>
        public void Initialize(CanvasViewContext viewContext)
        {
            if (grid == null || background == null || background.sprite == null || terrainOverlay == null || route == null || heroes == null
                || towns == null || friendlyUnits == null || enemies == null || markerPrefab == null || routeStepPrefab == null || hoverCell == null)
            {
                throw new InvalidOperationException($"AdventureMapView on '{name}' has unassigned references. Assign them in the map prefab.");
            }

            context = viewContext;
            terrainOverlay.enabled = false;
            hoverCell.gameObject.SetActive(false);
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

            Refresh();
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
            RefreshHero();
            RefreshRoute();
        }

        public void Tick(float deltaTime)
        {
            heroMarker.Tick(deltaTime);
        }

        public void ShowHover(WorldPosition position)
        {
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
                // authored art keeps its colours; placeholder icons show the side
                Color tint = context.Media.Unit(leaderId) != null ? Color.white : side;
                marker.Display(context.UnitIcon(leaderId), tint, BaseColor(side), ArmySize(encounter.Army).ToString());
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
