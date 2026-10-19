using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The orthographic camera that draws the world inside a hole of the UI (a viewport RectTransform). It follows,
    /// pans and shakes, and always stays inside the bounds it was given; when the bounds are smaller than the view they
    /// are centred instead. There is no zoom yet: the size comes from the content's cell size.
    /// </summary>
    public sealed class WorldCamera : MonoBehaviour
    {
        private const float FOLLOW_SECONDS = 0.2f;
        private const float SHAKE_SECONDS = 0.3f;
        private const float SHAKE_DISTANCE = 0.12f;
        private const float CAMERA_DEPTH = -10f;

        [SerializeField] private Camera targetCamera;

        private readonly Vector3[] corners = new Vector3[4];
        private Rect bounds;
        private Vector2 center;
        private Vector2 followVelocity;
        private float shakeTime;
        private float shakeSeconds = SHAKE_SECONDS;
        private float shakeDistance = SHAKE_DISTANCE;

        public Camera Camera => targetCamera;
        public Vector2 Center => center;

        /// <summary>World units covered by one screen pixel, for dragging the map with the pointer.</summary>
        public float WorldUnitsPerPixel => targetCamera.pixelHeight > 0 ? targetCamera.orthographicSize * 2f / targetCamera.pixelHeight : 0f;

        /// <summary>The world area the camera shows, without shake.</summary>
        public Rect VisibleArea
        {
            get
            {
                float halfHeight = targetCamera.orthographicSize;
                float halfWidth = halfHeight * targetCamera.aspect;
                return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
            }
        }

        public void SetVisible(bool isVisible)
        {
            targetCamera.enabled = isVisible;
        }

        /// <summary>Draws into the screen area of <paramref name="viewport"/> (a UI RectTransform on a screen-space overlay canvas).</summary>
        public void SetViewport(RectTransform viewport, float orthographicSize, Rect worldBounds)
        {
            viewport.GetWorldCorners(corners);
            Rect pixels = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
            if (targetCamera.pixelRect != pixels)
            {
                targetCamera.pixelRect = pixels;
            }

            targetCamera.orthographicSize = orthographicSize;
            bounds = worldBounds;
            center = Clamp(center);
        }

        public void CenterOn(Vector2 point)
        {
            center = Clamp(point);
            followVelocity = Vector2.zero;
        }

        /// <summary>Moves smoothly towards <paramref name="point"/>; call every frame while following.</summary>
        public void Follow(Vector2 point, float deltaTime)
        {
            center = Clamp(Vector2.SmoothDamp(center, point, ref followVelocity, FOLLOW_SECONDS, Mathf.Infinity, deltaTime));
        }

        public void Pan(Vector2 worldDelta)
        {
            center = Clamp(center + worldDelta);
            followVelocity = Vector2.zero;
        }

        public void Shake()
        {
            Shake(SHAKE_SECONDS, SHAKE_DISTANCE);
        }

        /// <param name="distance">World units at the start, calming down over the seconds.</param>
        public void Shake(float seconds, float distance)
        {
            shakeSeconds = Mathf.Max(0.01f, seconds);
            shakeDistance = Mathf.Max(0f, distance);
            shakeTime = shakeSeconds;
        }

        public bool ContainsScreenPoint(Vector2 screenPoint)
        {
            return targetCamera.enabled && targetCamera.pixelRect.Contains(screenPoint);
        }

        public Vector3 ScreenToWorld(Vector2 screenPoint)
        {
            Vector3 point = targetCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, -CAMERA_DEPTH));
            point.z = 0f;
            return point;
        }

        public Vector2 WorldToScreen(Vector3 worldPoint)
        {
            return targetCamera.WorldToScreenPoint(worldPoint);
        }

        /// <summary>Applies the position, including shake, after the views have moved this frame.</summary>
        public void Apply(float deltaTime)
        {
            Vector2 shake = Vector2.zero;
            if (shakeTime > 0f)
            {
                shakeTime = Mathf.Max(0f, shakeTime - deltaTime);
                shake = Random.insideUnitCircle * (shakeDistance * shakeTime / shakeSeconds);
            }

            transform.position = new Vector3(center.x + shake.x, center.y + shake.y, CAMERA_DEPTH);
        }

        private Vector2 Clamp(Vector2 point)
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            return new Vector2(ClampAxis(point.x, bounds.xMin, bounds.xMax, halfWidth), ClampAxis(point.y, bounds.yMin, bounds.yMax, halfHeight));
        }

        private static float ClampAxis(float value, float minimum, float maximum, float halfView)
        {
            if (maximum - minimum <= halfView * 2f)
            {
                return (minimum + maximum) * 0.5f;
            }

            return Mathf.Clamp(value, minimum + halfView, maximum - halfView);
        }
    }
}
