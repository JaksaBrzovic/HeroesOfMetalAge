using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Application;
using HeroesOfMetalAge.Domain;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Root of everything drawn in the world (the UI stays in <see cref="GameHud"/>): shows the adventure map or the
    /// battlefield for the session state, points the camera at it through the screen's viewport, and turns pointer and
    /// arrow-key input into map and battle commands. World input obeys the same blocking as the UI: no clicks under
    /// dialogs, options or the development panel, and none through UI controls.
    /// </summary>
    public sealed class GameWorld : MonoBehaviour
    {
        private const float DRAG_THRESHOLD_PIXELS = 6f;
        private const float PAN_CELLS_PER_SECOND = 12f;
        private const float FOLLOW_SETTLE_SECONDS = 0.6f;
        // each notch of the mouse wheel scales the cell size by this factor
        private const float ZOOM_STEP = 1.15f;

        [SerializeField] private WorldCamera worldCamera;
        [Tooltip("Parent of the mission's adventure map instance.")]
        [SerializeField] private Transform maps;
        [SerializeField] private BattlefieldView battlefield;

        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private GameHud hud;
        private AdventureMapView map;
        private long shownRevision = -1;
        private GameScreen shownScreen;
        private bool hasShownScreen;
        private WorldPosition followedHeroPosition;
        private float followTime;
        private Vector2 pressPosition;
        private Vector2 lastPointerPosition;
        private bool isPressed;
        private bool isDragging;
        private float mapPixelsPerCell;

        public Transform Maps => maps;
        public AdventureMapView Map => map;
        public BattlefieldView Battlefield => battlefield;
        public WorldCamera Camera => worldCamera;
        /// <summary>The map cell's size on screen now, in UI design pixels; the mouse wheel zooms it.</summary>
        public float MapPixelsPerCell => mapPixelsPerCell;

        private GameSession Session => hud.Context.Session;

        /// <exception cref="InvalidOperationException">A reference is missing on the World prefab or the map prefab.</exception>
        public void Initialize(GameHud gameHud, AdventureMapView adventureMap)
        {
            if (worldCamera == null || maps == null || battlefield == null || adventureMap == null)
            {
                throw new InvalidOperationException($"GameWorld on '{name}' has unassigned references. Assign them in the World prefab.");
            }

            hud = gameHud;
            map = adventureMap;
            map.Initialize(hud.Context);
            mapPixelsPerCell = map.DisplayPixelsPerCell;
            battlefield.Initialize(hud.Context);
            followedHeroPosition = Session.Campaign.Position;
            LateUpdate();
        }

        public void ToggleTerrainOverlay()
        {
            map.SetTerrainOverlayVisible(!map.IsTerrainOverlayVisible);
        }

        /// <summary>
        /// A click at a screen point, as the pointer would make it: ignored outside the world view, over UI controls or while
        /// gameplay input is blocked. The hero's own cell opens the hero; another cell plans or rides; during the ride any cell
        /// picks a new destination (the hero's own cell stops him); a hex moves or attacks.
        /// </summary>
        /// <returns>True when the click reached a map cell or hex.</returns>
        public bool HandleClick(Vector2 screenPoint)
        {
            if (!hud.IsGameplayInputEnabled || !worldCamera.ContainsScreenPoint(screenPoint) || IsOverUi(screenPoint))
            {
                return false;
            }

            Vector3 point = worldCamera.ScreenToWorld(screenPoint);
            if (Session.Screen == GameScreen.Adventure && map.TryGetCell(point, out WorldPosition cell))
            {
                if (cell == Session.Campaign.Position && !Session.IsTraveling)
                {
                    Session.ShowHero();
                }
                else
                {
                    Session.Plan(cell);
                }

                return true;
            }

            if (Session.Screen == GameScreen.Battle && battlefield.TryGetHex(point, out HexPosition hex))
            {
                // a spell picked in the spell book is cast at the clicked hex
                if (Session.TargetingSpellId != null)
                {
                    Session.CastAt(hex);
                }
                else
                {
                    Session.BattleAction(new BattleAction(BattleActionKind.MoveOrAttack, hex));
                }

                return true;
            }

            return false;
        }

        public Vector2 ScreenPoint(WorldPosition position)
        {
            return worldCamera.WorldToScreen(map.CellCenterWorld(position));
        }

        public Vector2 ScreenPoint(HexPosition position)
        {
            return worldCamera.WorldToScreen(battlefield.HexCenterWorld(position));
        }

        public void CenterOn(WorldPosition position)
        {
            worldCamera.CenterOn(map.CellCenterWorld(position));
        }

        private void Update()
        {
            if (hud == null || !hud.IsDrivingSession)
            {
                return;
            }

            if (!hud.IsGameplayInputEnabled)
            {
                isPressed = false;
                isDragging = false;
                map.HideHover();
                return;
            }

            if (Session.Screen == GameScreen.Adventure)
            {
                HandlePanKeys();
                HandleZoom();
                HandleHover();
            }

            HandlePointer();
        }

        private void LateUpdate()
        {
            if (hud == null)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            GameScreen screen = Session.Screen;
            bool isMap = screen == GameScreen.Adventure;
            bool isBattle = screen == GameScreen.Battle;
            UiBinding.SetVisible(map.gameObject, isMap);
            UiBinding.SetVisible(battlefield.gameObject, isBattle);
            worldCamera.SetVisible(isMap || isBattle);

            bool isScreenChanged = !hasShownScreen || shownScreen != screen;
            shownScreen = screen;
            hasShownScreen = true;
            if (isScreenChanged || shownRevision != Session.Revision)
            {
                if (isMap)
                {
                    map.Refresh();
                }
                else if (isBattle)
                {
                    battlefield.Refresh();
                }

                shownRevision = Session.Revision;
            }

            if (isMap)
            {
                ShowMap(isScreenChanged, deltaTime);
            }
            else if (isBattle)
            {
                ShowBattle(deltaTime);
            }

            worldCamera.Apply(deltaTime);
        }

        private void ShowMap(bool isScreenChanged, float deltaTime)
        {
            map.Tick(deltaTime);
            RectTransform viewport = hud.WorldView.MapViewport;
            Rect bounds = map.Bounds;
            bounds.position += (Vector2)map.transform.position;
            worldCamera.SetViewport(viewport, viewport.rect.height / (2f * mapPixelsPerCell), bounds);

            Vector2 hero = map.HeroMarker.transform.position;
            if (isScreenChanged)
            {
                worldCamera.CenterOn(hero);
                followTime = 0f;
                followedHeroPosition = Session.Campaign.Position;
                return;
            }

            // the camera follows the hero while he travels, then leaves the view to the player
            if (followedHeroPosition != Session.Campaign.Position)
            {
                followedHeroPosition = Session.Campaign.Position;
                followTime = FOLLOW_SETTLE_SECONDS;
            }

            if (map.HeroMarker.IsMoving || Session.IsBusy)
            {
                followTime = FOLLOW_SETTLE_SECONDS;
            }

            if (followTime > 0f)
            {
                followTime -= deltaTime;
                worldCamera.Follow(hero, deltaTime);
            }
        }

        private void ShowBattle(float deltaTime)
        {
            battlefield.Tick(deltaTime);
            RectTransform viewport = hud.CombatView.BattleViewport;
            Rect frame = battlefield.FrameBounds;
            float aspect = viewport.rect.width / Mathf.Max(1f, viewport.rect.height);
            float size = Mathf.Max(frame.height * 0.5f, frame.width * 0.5f / aspect);
            worldCamera.SetViewport(viewport, size, frame);
            worldCamera.CenterOn(frame.center);
            if (battlefield.TakeShake() > 0f)
            {
                worldCamera.Shake();
            }
        }

        /// <summary>Sets the map cell's size on screen, keeping the world point under <paramref name="screenPoint"/> in place.</summary>
        public void Zoom(float pixelsPerCell, Vector2 screenPoint)
        {
            float target = Mathf.Clamp(pixelsPerCell, map.MinimumPixelsPerCell, map.MaximumPixelsPerCell);
            if (Mathf.Approximately(target, mapPixelsPerCell))
            {
                return;
            }

            Vector3 before = worldCamera.ScreenToWorld(screenPoint);
            mapPixelsPerCell = target;
            RectTransform viewport = hud.WorldView.MapViewport;
            Rect bounds = map.Bounds;
            bounds.position += (Vector2)map.transform.position;
            worldCamera.SetViewport(viewport, viewport.rect.height / (2f * mapPixelsPerCell), bounds);
            worldCamera.Apply(0f);
            Vector3 after = worldCamera.ScreenToWorld(screenPoint);
            worldCamera.Pan(before - after);
            followTime = 0f;
        }

        private void HandleZoom()
        {
            float wheel = Input.mouseScrollDelta.y;
            Vector2 pointer = Input.mousePosition;
            if (wheel == 0f || !worldCamera.ContainsScreenPoint(pointer) || IsOverUi(pointer))
            {
                return;
            }

            Zoom(mapPixelsPerCell * Mathf.Pow(ZOOM_STEP, wheel), pointer);
        }

        private void HandlePanKeys()
        {
            Vector2 direction = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                direction.x -= 1f;
            }

            if (Input.GetKey(KeyCode.RightArrow))
            {
                direction.x += 1f;
            }

            if (Input.GetKey(KeyCode.DownArrow))
            {
                direction.y -= 1f;
            }

            if (Input.GetKey(KeyCode.UpArrow))
            {
                direction.y += 1f;
            }

            if (direction != Vector2.zero)
            {
                followTime = 0f;
                worldCamera.Pan(direction.normalized * (PAN_CELLS_PER_SECOND * Time.unscaledDeltaTime));
            }
        }

        private void HandleHover()
        {
            Vector2 pointer = Input.mousePosition;
            if (!worldCamera.ContainsScreenPoint(pointer) || IsOverUi(pointer) || isDragging
                || !map.TryGetCell(worldCamera.ScreenToWorld(pointer), out WorldPosition cell))
            {
                map.HideHover();
                return;
            }

            map.ShowHover(cell);
        }

        // left button: click, or drag beyond a few pixels to pan the map; right and middle buttons always drag
        private void HandlePointer()
        {
            Vector2 pointer = Input.mousePosition;
            bool isMap = Session.Screen == GameScreen.Adventure;
            if (Input.GetMouseButtonDown(0) && worldCamera.ContainsScreenPoint(pointer) && !IsOverUi(pointer))
            {
                isPressed = true;
                isDragging = false;
                pressPosition = pointer;
            }

            bool isDragButton = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (isMap && isPressed && !isDragging && (pointer - pressPosition).magnitude > DRAG_THRESHOLD_PIXELS)
            {
                isDragging = true;
            }

            if (isMap && (isDragging || isDragButton) && worldCamera.ContainsScreenPoint(lastPointerPosition))
            {
                followTime = 0f;
                worldCamera.Pan((lastPointerPosition - pointer) * worldCamera.WorldUnitsPerPixel);
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isPressed && !isDragging)
                {
                    HandleClick(pointer);
                }

                isPressed = false;
                isDragging = false;
            }

            lastPointerPosition = pointer;
        }

        private bool IsOverUi(Vector2 screenPoint)
        {
            PointerEventData pointer = new PointerEventData(hud.Events) { position = screenPoint };
            uiHits.Clear();
            hud.Events.RaycastAll(pointer, uiHits);
            return uiHits.Count > 0;
        }
    }
}
