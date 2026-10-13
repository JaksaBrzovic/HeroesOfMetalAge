using System;
using System.Collections.Generic;
using HeroesOfMetalAge.Domain;
using UnityEngine;

namespace HeroesOfMetalAge.Infrastructure.Content
{
    /// <summary>How a terrain type behaves on the adventure map.</summary>
    [Serializable]
    public sealed class TerrainEntry
    {
        [SerializeField] private TerrainType terrain;
        [Tooltip("False for terrain the mounted hero cannot ride through (forest, mountain, water, lava, obstacles).")]
        [SerializeField] private bool isPassable;
        [Tooltip("Movement points to enter one cell in a straight step (a hero has about 1600 per day).")]
        [Min(1)]
        [SerializeField] private int movementCost = 100;

        public TerrainEntry(TerrainType terrain, bool isPassable, int movementCost)
        {
            this.terrain = terrain;
            this.isPassable = isPassable;
            this.movementCost = movementCost;
        }

        public TerrainType Terrain => terrain;
        public bool IsPassable => isPassable;
        public int MovementCost => movementCost;
    }

    /// <summary>
    /// Movement rules shared by every map: which terrain is passable, what each costs and how much more a diagonal step
    /// costs. Unpainted cells (<see cref="TerrainType.Blocked"/>) are always impassable.
    /// </summary>
    [CreateAssetMenu(menuName = "Heroes of Metal Age/Terrain Database")]
    public sealed class TerrainDatabaseScriptableObject : ScriptableObject
    {
        [SerializeField] private List<TerrainEntry> terrains = new List<TerrainEntry>();
        [Tooltip("A diagonal step costs this percentage of a straight one (141 = √2, as in classic strategy games).")]
        [Range(100, 200)]
        [SerializeField] private int diagonalCostPercent = 141;

        public IReadOnlyList<TerrainEntry> Terrains => terrains;
        public int DiagonalCostPercent => diagonalCostPercent;

        internal void Configure(IEnumerable<TerrainEntry> entries, int diagonalPercent)
        {
            terrains = new List<TerrainEntry>(entries);
            diagonalCostPercent = diagonalPercent;
        }

        public TerrainRules ToRules()
        {
            Dictionary<TerrainType, int> costs = new Dictionary<TerrainType, int>();
            foreach (TerrainEntry entry in terrains)
            {
                if (entry.IsPassable && entry.Terrain != TerrainType.Blocked)
                {
                    costs.Add(entry.Terrain, entry.MovementCost);
                }
            }

            return new TerrainRules(costs, diagonalCostPercent);
        }
    }
}
