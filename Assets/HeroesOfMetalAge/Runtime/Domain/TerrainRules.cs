using System.Collections.Generic;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Movement rules of the adventure map: which terrain the hero can ride over and how many movement points entering a cell
    /// of it costs. A diagonal step costs <see cref="DiagonalCostPercent"/> percent of that; terrain without a cost is
    /// impassable.
    /// </summary>
    public sealed class TerrainRules
    {
        private readonly Dictionary<TerrainType, int> movementCosts;

        public TerrainRules(IReadOnlyDictionary<TerrainType, int> passableCosts, int diagonalCostPercent)
        {
            movementCosts = new Dictionary<TerrainType, int>();
            foreach (KeyValuePair<TerrainType, int> cost in passableCosts)
            {
                movementCosts.Add(cost.Key, cost.Value);
            }

            DiagonalCostPercent = diagonalCostPercent;
        }

        public int DiagonalCostPercent { get; }

        public bool IsPassable(TerrainType terrain)
        {
            return movementCosts.ContainsKey(terrain);
        }

        /// <summary>Movement points to enter a cell of passable <paramref name="terrain"/> in a straight step.</summary>
        public int Cost(TerrainType terrain)
        {
            return movementCosts[terrain];
        }

        public int StepCost(TerrainType terrain, bool isDiagonal)
        {
            int cost = Cost(terrain);
            return isDiagonal ? cost * DiagonalCostPercent / 100 : cost;
        }
    }
}
