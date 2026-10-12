using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Infrastructure.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HeroesOfMetalAge.EditorTools
{
    /// <summary>
    /// Rebuild of an adventure map prefab after its image or cell size changed: imports the image as a sprite with one cell
    /// per world unit, cuts the grid to whole cells and keeps the painted logic layer. When the cell size changes, every new
    /// cell takes the terrain painted under its centre, so painted areas keep their shape on the image.
    /// </summary>
    public static class AdventureMapBuilder
    {
        private const int MAX_TEXTURE_SIZE = 16384;

        /// <summary>Rebuilds a map selected as a prefab asset, inside Prefab Mode or in a scene.</summary>
        /// <exception cref="InvalidOperationException">The map has no image or logic layer.</exception>
        public static void Rebuild(AdventureMapAuthoring map)
        {
            if (!PrefabUtility.IsPartOfPrefabAsset(map))
            {
                Undo.RegisterFullObjectHierarchyUndo(map.gameObject, "Rebuild adventure map");
                Apply(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                return;
            }

            string path = AssetDatabase.GetAssetPath(map);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Apply(contents.GetComponent<AdventureMapAuthoring>());
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>Source size of the map image in pixels, before any import downscaling.</summary>
        public static Vector2Int ImageSize(Sprite image)
        {
            TextureImporter importer = Importer(image);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            return new Vector2Int(width, height);
        }

        /// <summary>Redraws the logic layer for a new cell size from what was painted under each new cell's centre.</summary>
        public static void Resample(Tilemap logic, int oldPixelsPerCell, int newPixelsPerCell, int width, int height)
        {
            Dictionary<Vector2Int, TileBase> painted = Read(logic);
            logic.ClearAllTiles();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int source = new Vector2Int(
                        Mathf.FloorToInt((x + 0.5f) * newPixelsPerCell / oldPixelsPerCell),
                        Mathf.FloorToInt((y + 0.5f) * newPixelsPerCell / oldPixelsPerCell));
                    if (painted.TryGetValue(source, out TileBase tile))
                    {
                        logic.SetTile(new Vector3Int(x, y, 0), tile);
                    }
                }
            }

            logic.CompressBounds();
        }

        /// <summary>Removes painted cells that fall outside a map of the given size.</summary>
        public static void Crop(Tilemap logic, int width, int height)
        {
            foreach (KeyValuePair<Vector2Int, TileBase> cell in Read(logic))
            {
                if (cell.Key.x < 0 || cell.Key.y < 0 || cell.Key.x >= width || cell.Key.y >= height)
                {
                    logic.SetTile(new Vector3Int(cell.Key.x, cell.Key.y, 0), null);
                }
            }

            logic.CompressBounds();
        }

        private static void Apply(AdventureMapAuthoring map)
        {
            if (map.MapImage == null || map.TerrainLogic == null || map.Background == null)
            {
                throw new InvalidOperationException("Assign the map image, the background renderer and the terrain logic layer before rebuilding.");
            }

            int cellSize = map.PixelsPerCell;
            Vector2Int imageSize = ImportImage(map.MapImage, cellSize);
            int width = imageSize.x / cellSize;
            int height = imageSize.y / cellSize;

            if (map.BuiltPixelsPerCell > 0 && map.BuiltPixelsPerCell != cellSize)
            {
                Resample(map.TerrainLogic, map.BuiltPixelsPerCell, cellSize, width, height);
            }
            else
            {
                Crop(map.TerrainLogic, width, height);
            }

            // one cell per world unit, with the image's bottom-left corner on the first cell
            Grid grid = map.TerrainLogic.layoutGrid;
            grid.cellSize = Vector3.one;
            grid.transform.localPosition = Vector3.zero;
            map.Background.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(map.MapImage));
            map.Background.transform.localPosition = Vector3.zero;
            map.Background.transform.localScale = Vector3.one;
            map.ApplyBuild(cellSize, width, height);
            EditorUtility.SetDirty(map);
            EditorUtility.SetDirty(map.Background);
            EditorUtility.SetDirty(map.TerrainLogic);
            EditorUtility.SetDirty(grid);
            Debug.Log($"Rebuilt map '{map.name}': {imageSize.x}x{imageSize.y} px at {cellSize} px per cell = {width}x{height} cells.");
        }

        private static Vector2Int ImportImage(Sprite image, int pixelsPerCell)
        {
            TextureImporter importer = Importer(image);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = pixelsPerCell;
            settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.mipmapEnabled = false;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;
            importer.SetTextureSettings(settings);
            // the whole image must survive import; a downscaled texture would no longer match the cell size
            importer.maxTextureSize = Mathf.Min(MAX_TEXTURE_SIZE, Mathf.NextPowerOfTwo(Mathf.Max(width, height)));
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return new Vector2Int(width, height);
        }

        private static TextureImporter Importer(Sprite image)
        {
            string path = AssetDatabase.GetAssetPath(image);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                throw new InvalidOperationException("The map image must be an imported texture: " + path);
            }

            return importer;
        }

        private static Dictionary<Vector2Int, TileBase> Read(Tilemap logic)
        {
            Dictionary<Vector2Int, TileBase> painted = new Dictionary<Vector2Int, TileBase>();
            foreach (Vector3Int cell in logic.cellBounds.allPositionsWithin)
            {
                TileBase tile = logic.GetTile(cell);
                if (tile != null)
                {
                    painted[new Vector2Int(cell.x, cell.y)] = tile;
                }
            }

            return painted;
        }
    }
}
