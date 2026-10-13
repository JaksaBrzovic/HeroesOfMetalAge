using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The battlefield in the world: a fixed image with the hex board laid over it, the stacks and hit feedback. It shows
    /// what <see cref="BattleEngine"/> resolved and animates it (glides, lunges, flashes, damage numbers); animation never
    /// decides whose turn it is. Hexes are pointy-top, rows count from the top and odd rows shift right by half a hex.
    /// </summary>
    public sealed class BattlefieldView : MonoBehaviour
    {
        // pointy-top hexes: neighbouring rows are √3/2 of a hex width apart
        private const float ROW_SPACING = 0.8660254f;
        // distance from a hex centre to its corners, relative to its width
        private const float HEX_RADIUS = 0.5773503f;
        private const float UNIT_HEXES_PER_SECOND = 6f;
        private const float LUNGE_FRACTION = 0.3f;
        private const float MARKER_BASE_BLEND = 0.45f;
        private const float HEX_ALPHA = 0.6f;
        private const float REACHABLE_HEX_ALPHA = 0.8f;
        private const float ACTIVE_HEX_ALPHA = 0.85f;

        [SerializeField] private SpriteRenderer background;
        [Tooltip("Centre of the hex board, in this object's space. Select the object to preview the board in the Scene view.")]
        [SerializeField] private Vector2 boardCenter;
        [Tooltip("Width of one hex in world units.")]
        [Min(0.1f)]
        [SerializeField] private float hexWidth = 1.3f;
        [Tooltip("Space kept around the board when the camera frames it, in world units.")]
        [Min(0f)]
        [SerializeField] private float frameMargin = 0.4f;
        [Tooltip("Hits at or above this damage shake the camera.")]
        [Min(1)]
        [SerializeField] private int shakeDamageThreshold = 45;
        [Tooltip("Board size used only for the Scene view preview; the mission sets the real size.")]
        [SerializeField] private Vector2Int previewBoardSize = new Vector2Int(10, 7);

        [Header("Content")]
        [SerializeField] private Transform hexes;
        [SerializeField] private Transform playerUnits;
        [SerializeField] private Transform enemyUnits;
        [SerializeField] private Transform effects;
        [SerializeField] private SpriteRenderer hexPrefab;
        [SerializeField] private WorldMarker unitPrefab;
        [SerializeField] private FloatingText damageTextPrefab;

        private readonly Dictionary<int, WorldMarker> unitMarkers = new Dictionary<int, WorldMarker>();
        private readonly List<FloatingText> damageTexts = new List<FloatingText>();
        private CanvasViewContext context;
        private SpriteRenderer[,] cells;
        private BattleEngine displayedBattle;
        private Sprite prefabBackground;
        private int boardWidth;
        private int boardHeight;
        private float pendingShake;

        /// <summary>The board plus the frame margin, in world space; what the camera must keep in view.</summary>
        public Rect FrameBounds
        {
            get
            {
                Vector2 size = BoardSize(boardWidth, boardHeight) + Vector2.one * (frameMargin * 2f);
                Vector2 center = transform.TransformPoint(boardCenter);
                return new Rect(center - size * 0.5f, size);
            }
        }

        private GameSession Session => context.Session;

        /// <exception cref="InvalidOperationException">A reference is missing on the Battlefield prefab.</exception>
        public void Initialize(CanvasViewContext viewContext)
        {
            if (background == null || hexes == null || playerUnits == null || enemyUnits == null || effects == null || hexPrefab == null
                || unitPrefab == null || damageTextPrefab == null)
            {
                throw new InvalidOperationException($"BattlefieldView on '{name}' has unassigned references. Assign them in the Battlefield prefab.");
            }

            context = viewContext;
            prefabBackground = background.sprite;

            CombatPolicy policy = Session.Catalog.Mission.Combat;
            boardWidth = policy.Width;
            boardHeight = policy.Height;
            cells = new SpriteRenderer[boardWidth, boardHeight];
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    SpriteRenderer cell = Instantiate(hexPrefab, hexes);
                    cell.name = "Hex " + x + "," + y;
                    cell.transform.localPosition = HexCenter(new HexPosition(x, y));
                    cell.transform.localScale = Vector3.one * hexWidth;
                    cells[x, y] = cell;
                }
            }

            Session.DamageResolved += HandleDamageResolved;
        }

        public WorldMarker UnitMarker(int combatantId)
        {
            return unitMarkers[combatantId];
        }

        /// <summary>Centre of a hex in this object's space.</summary>
        public Vector3 HexCenter(HexPosition position)
        {
            return HexCenter(position, boardWidth, boardHeight);
        }

        public Vector3 HexCenterWorld(HexPosition position)
        {
            return transform.TransformPoint(HexCenter(position));
        }

        /// <summary>The hex under a world point, if the point is inside one.</summary>
        public bool TryGetHex(Vector3 worldPoint, out HexPosition position)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            float best = float.MaxValue;
            position = default;
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    HexPosition candidate = new HexPosition(x, y);
                    float distance = ((Vector2)(HexCenter(candidate) - local)).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        position = candidate;
                    }
                }
            }

            float radius = hexWidth * HEX_RADIUS;
            return best <= radius * radius;
        }

        /// <summary>Camera shake requested by heavy hits since the last call.</summary>
        public float TakeShake()
        {
            float shake = pendingShake;
            pendingShake = 0f;
            return shake;
        }

        public void Refresh()
        {
            BattleEngine battle = Session.Battle;
            if (battle == null)
            {
                return;
            }

            if (displayedBattle != battle)
            {
                AssignUnitMarkers(battle);
            }

            Combatant active = battle.Active;
            bool canAct = active.IsPlayer && !Session.IsBusy;
            for (int y = 0; y < boardHeight; y++)
            {
                for (int x = 0; x < boardWidth; x++)
                {
                    cells[x, y].color = HexColor(battle, active, new HexPosition(x, y), canAct);
                }
            }

            foreach (Combatant combatant in battle.Combatants)
            {
                RefreshUnit(combatant);
            }
        }

        public void Tick(float deltaTime)
        {
            foreach (WorldMarker marker in unitMarkers.Values)
            {
                marker.Tick(deltaTime);
            }

            foreach (FloatingText text in damageTexts)
            {
                text.Tick(deltaTime);
            }
        }

        private void OnDestroy()
        {
            if (context != null)
            {
                Session.DamageResolved -= HandleDamageResolved;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int y = 0; y < previewBoardSize.y; y++)
            {
                for (int x = 0; x < previewBoardSize.x; x++)
                {
                    DrawHexGizmo(HexCenter(new HexPosition(x, y), previewBoardSize.x, previewBoardSize.y));
                }
            }
        }

        /// <summary>A new battle reuses the markers by combatant ID and places them without animating.</summary>
        private void AssignUnitMarkers(BattleEngine battle)
        {
            Sprite battleground = context.Media.Battleground(Session.Encounter.Id);
            background.sprite = battleground != null ? battleground : prefabBackground;
            foreach (WorldMarker previous in unitMarkers.Values)
            {
                previous.gameObject.SetActive(false);
            }

            foreach (Combatant combatant in battle.Combatants)
            {
                Transform parent = combatant.IsPlayer ? playerUnits : enemyUnits;
                if (!unitMarkers.TryGetValue(combatant.Id, out WorldMarker marker))
                {
                    marker = Instantiate(unitPrefab, parent);
                    marker.transform.localScale = Vector3.one * hexWidth;
                    unitMarkers.Add(combatant.Id, marker);
                }

                marker.transform.SetParent(parent, false);
                marker.Show();
                marker.Place(HexCenter(combatant.Position));
            }

            displayedBattle = battle;
        }

        private void RefreshUnit(Combatant combatant)
        {
            WorldMarker marker = unitMarkers[combatant.Id];
            if (!combatant.IsAlive)
            {
                marker.FadeOut();
                return;
            }

            UnitDefinition unit = Session.Catalog.Unit(combatant.UnitId);
            Color side = combatant.IsPlayer ? context.Theme.Gold : context.Theme.Enemy;
            marker.name = (combatant.IsPlayer ? "Player " : "Enemy ") + unit.Name;
            marker.Display(context.UnitIcon(combatant.UnitId), context.UnitTint(combatant.UnitId, combatant.IsPlayer), BaseColor(side),
                combatant.Count.ToString());
            FigureAnimation animation = context.Media.UnitAnimation(combatant.UnitId);
            marker.SetFrames(animation.Frames, animation.FramesPerSecond);
            marker.SetFacing(!combatant.IsPlayer, false);
            marker.SetSelected(combatant == Session.Battle.Active, side);
            marker.MoveTo(HexCenter(combatant.Position), UNIT_HEXES_PER_SECOND * hexWidth);
        }

        private Color HexColor(BattleEngine battle, Combatant active, HexPosition position, bool canAct)
        {
            Color color;
            if (battle.At(position) == active)
            {
                // the acting stack's hex stands out in its side's colour
                color = active.IsPlayer ? context.Theme.Gold : context.Theme.Enemy;
                color.a = ACTIVE_HEX_ALPHA;
                return color;
            }

            if (canAct && battle.IsReachable(position))
            {
                color = context.Theme.ReachableHexColor;
                color.a = REACHABLE_HEX_ALPHA;
                return color;
            }
            else
            {
                color = context.Theme.HexColor;
            }

            color.a = HEX_ALPHA;
            return color;
        }

        private void HandleDamageResolved(DamageEvent hit)
        {
            if (!unitMarkers.TryGetValue(hit.AttackerCombatantId, out WorldMarker attacker)
                || !unitMarkers.TryGetValue(hit.TargetCombatantId, out WorldMarker target))
            {
                return;
            }

            attacker.Lunge((target.Target - attacker.Target) * LUNGE_FRACTION);
            Vector3 textPosition = target.Target + Vector3.up * (hexWidth * 0.35f);
            if (hit.IsMiss)
            {
                NextDamageText().Show("MISS", context.Theme.Muted, textPosition);
                return;
            }

            target.Flash();
            Color color = hit.IsReceivedByPlayer ? context.Theme.Enemy : context.Theme.Gold;
            NextDamageText().Show("-" + hit.Damage, color, textPosition);
            if (hit.Damage >= shakeDamageThreshold)
            {
                pendingShake = Mathf.Max(pendingShake, 1f);
            }
        }

        private FloatingText NextDamageText()
        {
            foreach (FloatingText text in damageTexts)
            {
                if (!text.IsShowing)
                {
                    return text;
                }
            }

            FloatingText created = Instantiate(damageTextPrefab, effects);
            damageTexts.Add(created);
            return created;
        }

        private Vector3 HexCenter(HexPosition position, int width, int height)
        {
            Vector2 size = BoardSize(width, height);
            float left = boardCenter.x - size.x * 0.5f + hexWidth * 0.5f;
            float top = boardCenter.y + size.y * 0.5f - hexWidth * HEX_RADIUS;
            float x = left + (position.X + (position.Y & 1) * 0.5f) * hexWidth;
            float y = top - position.Y * hexWidth * ROW_SPACING;
            return new Vector3(x, y, 0f);
        }

        // the extent of the hexes themselves: odd rows add half a hex, the top and bottom rows add their corners
        private Vector2 BoardSize(int width, int height)
        {
            float boardWidthUnits = (width + (height > 1 ? 0.5f : 0f)) * hexWidth;
            float boardHeightUnits = (height - 1) * hexWidth * ROW_SPACING + 2f * hexWidth * HEX_RADIUS;
            return new Vector2(boardWidthUnits, boardHeightUnits);
        }

        private Color BaseColor(Color side)
        {
            Color color = Color.Lerp(context.Theme.Panel, side, MARKER_BASE_BLEND);
            color.a = 0.92f;
            return color;
        }

        private void DrawHexGizmo(Vector3 center)
        {
            float radius = hexWidth * HEX_RADIUS;
            for (int corner = 0; corner < 6; corner++)
            {
                float from = Mathf.Deg2Rad * (60f * corner + 30f);
                float to = Mathf.Deg2Rad * (60f * (corner + 1) + 30f);
                Gizmos.DrawLine(center + new Vector3(Mathf.Cos(from), Mathf.Sin(from)) * radius, center + new Vector3(Mathf.Cos(to), Mathf.Sin(to)) * radius);
            }
        }
    }
}
