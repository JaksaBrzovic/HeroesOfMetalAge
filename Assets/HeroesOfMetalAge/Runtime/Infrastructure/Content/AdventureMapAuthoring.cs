using HeroesOfMetalAge.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// Authoring data of an adventure map prefab: the painted map image and the logic layer painted over it. The image is
    /// cut into square cells of <see cref="PixelsPerCell"/> pixels; Rebuild in the Inspector applies a changed image or
    /// cell size. The logic layer is the only source of terrain for the rules; unpainted cells are impassable.
    /// </summary>
    public sealed class AdventureMapAuthoring : MonoBehaviour
    {
        [Tooltip("Shows the map image; its sprite is set by Rebuild.")]
        [SerializeField] private SpriteRenderer background;
        [Tooltip("The image to cut into cells. After changing it or the cell size, press Rebuild.")]
        [SerializeField] private Sprite mapImage;
        [Tooltip("Image pixels per map cell (cells are square).")]
        [Min(8)]
        [SerializeField] private int pixelsPerCell = 64;
        [Tooltip("Logic layer: paint terrain tiles over the walkable parts of the image (Tile Palette > Terrain Logic).")]
        [SerializeField] private Tilemap terrainLogic;

        [Header("Last rebuild (read only)")]
        [SerializeField] private int builtPixelsPerCell;
        [SerializeField] private int width;
        [SerializeField] private int height;

        public SpriteRenderer Background => background;
        public Sprite MapImage => mapImage;
        public int PixelsPerCell => pixelsPerCell;
        public Tilemap TerrainLogic => terrainLogic;
        public int BuiltPixelsPerCell => builtPixelsPerCell;
        public int Width => width;
        public int Height => height;

        /// <summary>True when the last rebuild matches the current image and cell size.</summary>
        public bool IsBuilt => builtPixelsPerCell == pixelsPerCell
            && mapImage != null
            && background != null
            && background.sprite == mapImage
            && Mathf.Approximately(mapImage.pixelsPerUnit, pixelsPerCell)
            && width == Mathf.FloorToInt(mapImage.rect.width / pixelsPerCell)
            && height == Mathf.FloorToInt(mapImage.rect.height / pixelsPerCell);

        /// <summary>
        /// Terrain of every cell, row by row from the top-left corner: the order and orientation of <see cref="WorldPosition"/>.
        /// Tilemap rows count up from the bottom, so they are flipped here.
        /// </summary>
        public TerrainType[] ReadTerrain()
        {
            TerrainType[] cells = new TerrainType[width * height];
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    TerrainTile tile = terrainLogic.GetTile<TerrainTile>(new Vector3Int(x, height - 1 - row, 0));
                    cells[row * width + x] = tile != null ? tile.Terrain : TerrainType.Blocked;
                }
            }

            return cells;
        }

        internal void ApplyBuild(int builtCellSize, int builtWidth, int builtHeight)
        {
            builtPixelsPerCell = builtCellSize;
            width = builtWidth;
            height = builtHeight;
        }

        internal void Configure(SpriteRenderer backgroundRenderer, Sprite image, int cellSize, Tilemap logic)
        {
            background = backgroundRenderer;
            mapImage = image;
            pixelsPerCell = cellSize;
            terrainLogic = logic;
        }
    }
}
