using System;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// The main menu's painted world in depth layers, from the sky to the rocks in front, seen through an imagined camera.
    /// The camera follows the mouse: towards the side the pointer is on, smoothly. Once the pointer has rested for a
    /// random 1 to 4 seconds the camera eases into a gentle sway, only left and right, like swaying in the wind (two slow
    /// waves of different periods beating against each other, with a little noise); as soon as the pointer moves, it
    /// takes the camera back. Each layer moves against the camera by its own factor (0 the sky, 1 the foreground), which
    /// gives the depth. Time is unscaled. Its own Canvas keeps the moving layers out of the menu buttons' rebuilds.
    /// </summary>
    public sealed class ParallaxView : MonoBehaviour
    {
        private const float SLOW_PERIOD = 19f;
        private const float SLOWER_PERIOD = 31f;
        private const float NOISE_SPEED = 0.23f;
        // pointer moves smaller than this (in screen fractions) do not count, so a jittery mouse still counts as resting
        private const float POINTER_DEAD_ZONE = 0.0005f;

        [Tooltip("Back to front; each with how strongly it moves against the camera.")]
        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [Tooltip("Furthest the pointer moves the camera from the middle, in canvas units of a layer with factor 1.")]
        [SerializeField] private Vector2 pointerAmplitude = new Vector2(110f, 12f);
        [Tooltip("Seconds the camera takes to catch up with the pointer.")]
        [Min(0.01f)]
        [SerializeField] private float followSeconds = 0.35f;
        [Tooltip("Furthest the resting camera sways to either side, in canvas units of a layer with factor 1.")]
        [Min(0f)]
        [SerializeField] private float swayAmplitude = 90f;
        [Tooltip("The pointer rests a random time in this range (seconds) before the sway starts.")]
        [SerializeField] private Vector2 idleDelayRange = new Vector2(1f, 4f);
        [Tooltip("Seconds the camera takes to ease between following the pointer and swaying.")]
        [Min(0.01f)]
        [SerializeField] private float blendSeconds = 1.5f;
        [Tooltip("The layers are drawn this much larger, so the pointer's small vertical moves never show an edge.")]
        [Range(1f, 1.2f)]
        [SerializeField] private float overscan = 1.03f;

        private readonly System.Random random = new System.Random();
        private Vector2[] homes = Array.Empty<Vector2>();
        private Vector2? lastPointer;
        private Vector2 pointerTarget;
        private Vector2 followed;
        private Vector2 followVelocity;
        private float idleSeconds;
        private float idleDelay;
        private float swaySeconds;
        private float swayWeight;

        public int LayerCount => layers.Length;
        /// <summary>The camera's current offset; a layer moves by minus this times its factor.</summary>
        public Vector2 CameraOffset { get; private set; }
        /// <summary>0 while the pointer leads, 1 while the camera sways on its own.</summary>
        public float SwayWeight => swayWeight;
        public Vector2 PointerAmplitude => pointerAmplitude;
        public float SwayAmplitude => swayAmplitude;
        public float Overscan => overscan;

        /// <exception cref="InvalidOperationException">A layer is missing its image on the FrontEnd prefab.</exception>
        public void Initialize()
        {
            if (layers.Length == 0 || Array.Exists(layers, layer => layer.Rect == null))
            {
                throw new InvalidOperationException($"ParallaxView on '{name}' needs its layers. Assign them in the FrontEnd prefab.");
            }

            if (homes.Length != layers.Length)
            {
                homes = new Vector2[layers.Length];
                for (int i = 0; i < layers.Length; i++)
                {
                    homes[i] = layers[i].Rect.anchoredPosition;
                }
            }

            transform.localScale = new Vector3(overscan, overscan, 1f);
            lastPointer = null;
            pointerTarget = Vector2.zero;
            followed = Vector2.zero;
            followVelocity = Vector2.zero;
            idleSeconds = 0f;
            idleDelay = NextIdleDelay();
            swaySeconds = 0f;
            swayWeight = 0f;
            Apply();
        }

        public RectTransform LayerRect(int index)
        {
            return layers[index].Rect;
        }

        public float LayerFactor(int index)
        {
            return layers[index].Factor;
        }

        /// <summary>The resting camera's sideways sway at a time of swaying: at most <paramref name="sway"/> to either side.</summary>
        public static float Drift(float time, float sway)
        {
            float slow = Mathf.Sin(time * Mathf.PI * 2f / SLOW_PERIOD + 0.7f);
            float slower = Mathf.Sin(time * Mathf.PI * 2f / SLOWER_PERIOD + 2.1f);
            float gusts = Mathf.PerlinNoise(time * NOISE_SPEED, 0.37f) * 2f - 1f;
            return sway * Mathf.Clamp(0.55f * slow + 0.3f * slower + 0.15f * gusts, -1f, 1f);
        }

        /// <param name="pointer">The pointer on the screen, 0 to 1 on both axes, or null when there is none.</param>
        internal void Tick(float deltaSeconds, Vector2? pointer)
        {
            float delta = Mathf.Max(0f, deltaSeconds);
            if (pointer.HasValue)
            {
                Vector2 position = new Vector2(Mathf.Clamp01(pointer.Value.x), Mathf.Clamp01(pointer.Value.y));
                if (lastPointer.HasValue && (position - lastPointer.Value).sqrMagnitude > POINTER_DEAD_ZONE * POINTER_DEAD_ZONE)
                {
                    // a new rest after this move waits a new random time
                    if (idleSeconds >= idleDelay)
                    {
                        idleDelay = NextIdleDelay();
                    }

                    idleSeconds = 0f;
                }

                lastPointer = position;
                pointerTarget = Vector2.Scale(position * 2f - Vector2.one, pointerAmplitude);
            }

            idleSeconds += delta;
            bool isResting = idleSeconds >= idleDelay;
            swayWeight = Mathf.MoveTowards(swayWeight, isResting ? 1f : 0f, delta / blendSeconds);
            if (swayWeight > 0f)
            {
                swaySeconds += delta;
            }

            followed = delta > 0f ? Vector2.SmoothDamp(followed, pointerTarget, ref followVelocity, followSeconds, Mathf.Infinity, delta) : followed;
            Apply();
        }

        private void Update()
        {
            if (homes.Length == layers.Length && layers.Length > 0)
            {
                Vector2? pointer = Input.mousePresent && Screen.width > 0 && Screen.height > 0
                    ? new Vector2(Input.mousePosition.x / Screen.width, Input.mousePosition.y / Screen.height)
                    : (Vector2?)null;
                Tick(Time.unscaledDeltaTime, pointer);
            }
        }

        // the sway is sideways only; while it leads, the pointer's height eases back to the middle
        private void Apply()
        {
            float blend = Mathf.SmoothStep(0f, 1f, swayWeight);
            CameraOffset = new Vector2(Mathf.Lerp(followed.x, Drift(swaySeconds, swayAmplitude), blend), followed.y * (1f - blend));
            for (int i = 0; i < layers.Length; i++)
            {
                layers[i].Rect.anchoredPosition = homes[i] - CameraOffset * layers[i].Factor;
            }
        }

        private float NextIdleDelay()
        {
            float min = Mathf.Min(idleDelayRange.x, idleDelayRange.y);
            float max = Mathf.Max(idleDelayRange.x, idleDelayRange.y);
            return min + (float)random.NextDouble() * (max - min);
        }

        [Serializable]
        private struct Layer
        {
            [SerializeField] private RectTransform rect;
            [Tooltip("0 stays still (the sky), 1 moves the most (the foreground).")]
            [Range(0f, 1.5f)]
            [SerializeField] private float factor;

            public RectTransform Rect => rect;
            public float Factor => factor;
        }
    }
}
