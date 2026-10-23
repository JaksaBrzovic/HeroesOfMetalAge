using System;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Presentation
{
    /// <summary>
    /// Draws the fog of war of the adventure map into the map prefab's fog renderer: a texture with a few texels per cell,
    /// dark over unexplored cells, with a soft, slightly ragged edge along the explored land. Redraws only when more of the
    /// map has been explored. Owns its texture and sprite; <see cref="Dispose"/> releases them.
    /// </summary>
    internal sealed class FogOfWarLayer : IDisposable
    {
        private const int TEXELS_PER_CELL = 4;
        // blended fog is 0.5 on the border between an explored and an unexplored cell; the edge lies a little inside the
        // unexplored cell so explored cells stay clear, and threshold + noise + softness stays below 1 so deep fog is opaque
        private const float EDGE_THRESHOLD = 0.7f;
        // half the width of the soft edge, in blended fog
        private const float EDGE_SOFTNESS = 0.15f;
        // how far the edge wavers, in blended fog, and the size of its waves in texels
        private const float EDGE_NOISE = 0.12f;
        private const int NOISE_PERIOD_TEXELS = 5;

        private static readonly Color32 FOG_COLOR = new Color32(8, 6, 5, 255);

        private readonly SpriteRenderer renderer;
        private readonly Texture2D texture;
        private readonly Sprite sprite;
        private readonly Color32[] texels;
        private readonly float[] fog;
        private readonly float[] noise;
        private readonly int width;
        private readonly int height;
        private ExploredMap shown;
        private int shownCount = -1;

        /// <param name="fogRenderer">Renderer placed at the map's bottom-left corner, one world unit per cell.</param>
        public FogOfWarLayer(SpriteRenderer fogRenderer, int mapWidth, int mapHeight)
        {
            renderer = fogRenderer;
            width = mapWidth;
            height = mapHeight;
            int texelWidth = width * TEXELS_PER_CELL;
            int texelHeight = height * TEXELS_PER_CELL;
            texture = new Texture2D(texelWidth, texelHeight, TextureFormat.RGBA32, false)
            {
                name = "FogOfWar",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texelWidth, texelHeight), Vector2.zero, TEXELS_PER_CELL, 0, SpriteMeshType.FullRect);
            sprite.name = "FogOfWar";
            renderer.sprite = sprite;
            texels = new Color32[texelWidth * texelHeight];
            fog = new float[width * height];
            noise = BuildNoise(texelWidth, texelHeight);
        }

        public void Show(ExploredMap explored)
        {
            if (explored == shown && explored.Count == shownCount)
            {
                return;
            }

            shown = explored;
            shownCount = explored.Count;
            renderer.enabled = !explored.IsComplete;
            if (explored.IsComplete)
            {
                return;
            }

            // fog per grid cell; grid rows count from the bottom, map rows from the top
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    fog[row * width + x] = explored.IsExplored(new WorldPosition(x, height - 1 - row)) ? 0f : 1f;
                }
            }

            Draw();
            texture.SetPixels32(texels);
            texture.Apply(false);
        }

        public void Dispose()
        {
            if (renderer != null)
            {
                renderer.sprite = null;
            }

            Release(sprite);
            Release(texture);
        }

        private static void Release(UnityEngine.Object owned)
        {
            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(owned);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(owned);
            }
        }

        // each texel blends the fog of the four nearest cell centres, then a smooth step with a wavering threshold turns that
        // into a soft but definite edge
        private void Draw()
        {
            int texelWidth = width * TEXELS_PER_CELL;
            int texelHeight = height * TEXELS_PER_CELL;
            for (int ty = 0; ty < texelHeight; ty++)
            {
                float v = (ty + 0.5f) / TEXELS_PER_CELL - 0.5f;
                int row = Mathf.Clamp(Mathf.FloorToInt(v), 0, height - 1);
                int nextRow = Mathf.Min(row + 1, height - 1);
                float fy = Mathf.Clamp01(v - row);
                for (int tx = 0; tx < texelWidth; tx++)
                {
                    float u = (tx + 0.5f) / TEXELS_PER_CELL - 0.5f;
                    int x = Mathf.Clamp(Mathf.FloorToInt(u), 0, width - 1);
                    int nextX = Mathf.Min(x + 1, width - 1);
                    float fx = Mathf.Clamp01(u - x);
                    float bottom = Mathf.Lerp(fog[row * width + x], fog[row * width + nextX], fx);
                    float top = Mathf.Lerp(fog[nextRow * width + x], fog[nextRow * width + nextX], fx);
                    float value = Mathf.Lerp(bottom, top, fy);
                    int index = ty * texelWidth + tx;
                    float threshold = EDGE_THRESHOLD + noise[index];
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(threshold - EDGE_SOFTNESS, threshold + EDGE_SOFTNESS, value));
                    Color32 color = FOG_COLOR;
                    color.a = (byte)Mathf.RoundToInt(alpha * 255f);
                    texels[index] = color;
                }
            }
        }

        /// <summary>Smooth value noise in [-EDGE_NOISE, EDGE_NOISE] per texel; the same for every run, so the edge never crawls.</summary>
        private static float[] BuildNoise(int texelWidth, int texelHeight)
        {
            float[] values = new float[texelWidth * texelHeight];
            for (int ty = 0; ty < texelHeight; ty++)
            {
                float gy = (float)ty / NOISE_PERIOD_TEXELS;
                int y0 = Mathf.FloorToInt(gy);
                float sy = Mathf.SmoothStep(0f, 1f, gy - y0);
                for (int tx = 0; tx < texelWidth; tx++)
                {
                    float gx = (float)tx / NOISE_PERIOD_TEXELS;
                    int x0 = Mathf.FloorToInt(gx);
                    float sx = Mathf.SmoothStep(0f, 1f, gx - x0);
                    float bottom = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), sx);
                    float top = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), sx);
                    values[ty * texelWidth + tx] = (Mathf.Lerp(bottom, top, sy) * 2f - 1f) * EDGE_NOISE;
                }
            }

            return values;
        }

        // a stable pseudo-random value in [0, 1] for a lattice point
        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }
    }
}
