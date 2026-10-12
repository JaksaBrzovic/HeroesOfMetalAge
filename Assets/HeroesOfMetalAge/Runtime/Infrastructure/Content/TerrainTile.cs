using HeroesOfMetalAge.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>
    /// A tile of the map's logic layer: which terrain the rules use for the cell it is painted on. Its colour only helps
    /// while painting; the logic layer is hidden in the game.
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Terrain Logic Tile")]
    public sealed class TerrainTile : Tile
    {
        [SerializeField] private TerrainType terrain;

        public TerrainType Terrain
        {
            get => terrain;
            internal set => terrain = value;
        }
    }
}
