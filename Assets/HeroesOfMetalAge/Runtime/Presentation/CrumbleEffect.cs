using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Breaks a simple Image into a grid of pieces that crack loose one after another, tumble and shrink into a point,
    /// as if they fell into a hole beneath. It only changes the Image's mesh: at <see cref="Progress"/> 0 the Image is
    /// drawn as usual, at 1 every piece is gone. Each piece's delay, drift and spin come from its index, so the same
    /// progress always draws the same picture.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class CrumbleEffect : BaseMeshEffect
    {
        [Min(1)]
        [SerializeField] private int columns = 14;
        [Min(1)]
        [SerializeField] private int rows = 4;
        [Tooltip("Where the pieces fall to, relative to the Image's pivot.")]
        [SerializeField] private Vector2 sinkPoint = new Vector2(0f, -8f);
        [Tooltip("Share of the progress over which the pieces start falling; the last starts here and falls in the rest.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float spread = 0.55f;
        [Tooltip("Degrees a piece turns at most while it falls.")]
        [SerializeField] private float spin = 220f;

        private readonly List<UIVertex> quad = new List<UIVertex>();
        private float progress;

        /// <summary>0 whole, 1 every piece fallen.</summary>
        public float Progress
        {
            get => progress;
            set
            {
                float clamped = Mathf.Clamp01(value);
                if (clamped == progress)
                {
                    return;
                }

                progress = clamped;
                if (graphic != null)
                {
                    graphic.SetVerticesDirty();
                }
            }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || progress <= 0f || vh.currentVertCount != 4)
            {
                return;
            }

            quad.Clear();
            for (int i = 0; i < 4; i++)
            {
                UIVertex vertex = default;
                vh.PopulateUIVertex(ref vertex, i);
                quad.Add(vertex);
            }

            // a simple Image's quad: bottom left, top left, top right, bottom right
            Vector3 min = quad[0].position;
            Vector3 max = quad[2].position;
            Vector4 uvMin = quad[0].uv0;
            Vector4 uvMax = quad[2].uv0;
            Color32 color = quad[0].color;
            vh.Clear();

            Vector2 cell = new Vector2((max.x - min.x) / columns, (max.y - min.y) / rows);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    float delay = spread * Mathf.Clamp01(Hash(index) * 0.7f + (1f - (float)row / rows) * 0.3f);
                    float t = Mathf.Clamp01((progress - delay) / (1f - spread));
                    if (t >= 1f)
                    {
                        continue;
                    }

                    Vector2 home = new Vector2(min.x + (column + 0.5f) * cell.x, min.y + (row + 0.5f) * cell.y);
                    // a short shake as it cracks loose, then an accelerating fall into the sink
                    float shake = t < 0.15f ? Mathf.Sin(t * 120f + index) * 1.5f * (1f - t / 0.15f) : 0f;
                    float fall = t * t;
                    Vector2 jitter = new Vector2(Hash(index + 101) - 0.5f, Hash(index + 211) - 0.5f) * 14f;
                    Vector2 center = Vector2.Lerp(home, sinkPoint + jitter, fall) + new Vector2(shake, -18f * Mathf.Sin(fall * Mathf.PI) * 0.5f);
                    float scale = Mathf.Lerp(1f, 0.08f, fall);
                    float angle = (Hash(index + 307) - 0.5f) * 2f * spin * fall * Mathf.Deg2Rad;
                    Color32 tint = color;
                    tint.a = (byte)(color.a * (1f - Mathf.SmoothStep(0.6f, 1f, t)));

                    Vector2 half = cell * 0.5f * scale;
                    float u0 = Mathf.Lerp(uvMin.x, uvMax.x, (float)column / columns);
                    float u1 = Mathf.Lerp(uvMin.x, uvMax.x, (float)(column + 1) / columns);
                    float v0 = Mathf.Lerp(uvMin.y, uvMax.y, (float)row / rows);
                    float v1 = Mathf.Lerp(uvMin.y, uvMax.y, (float)(row + 1) / rows);
                    int start = vh.currentVertCount;
                    AddCorner(vh, center, -half.x, -half.y, angle, new Vector2(u0, v0), tint);
                    AddCorner(vh, center, -half.x, half.y, angle, new Vector2(u0, v1), tint);
                    AddCorner(vh, center, half.x, half.y, angle, new Vector2(u1, v1), tint);
                    AddCorner(vh, center, half.x, -half.y, angle, new Vector2(u1, v0), tint);
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start + 2, start + 3, start);
                }
            }
        }

        private static void AddCorner(VertexHelper vh, Vector2 center, float x, float y, float angle, Vector2 uv, Color32 color)
        {
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            Vector3 position = new Vector3(center.x + x * cos - y * sin, center.y + x * sin + y * cos, 0f);
            vh.AddVert(position, color, uv);
        }

        // 0..1, the same for the same index
        private static float Hash(int index)
        {
            float value = Mathf.Sin(index * 12.9898f + 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
