using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Traversal model of the adventure map: terrain costs, blocking cells, encounter sites and routing.</summary>
    public sealed class WorldMap
    {
        private readonly MissionDefinition mission;
        private readonly Dictionary<WorldPosition, EncounterDefinition> sites;

        public WorldMap(MissionDefinition mission)
        {
            this.mission = mission;
            sites = mission.Encounters.ToDictionary(encounter => encounter.Position);
        }

        public int Width => mission.Width;
        public int Height => mission.Height;

        public bool IsInside(WorldPosition position)
        {
            return position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;
        }

        public TerrainType Terrain(WorldPosition position)
        {
            return mission.TerrainAt(position);
        }

        public bool IsWalkable(WorldPosition position)
        {
            if (!IsInside(position))
            {
                return false;
            }

            TerrainType terrain = Terrain(position);
            return terrain != TerrainType.Mountain && terrain != TerrainType.Water;
        }

        public int Cost(WorldPosition position)
        {
            return Terrain(position) == TerrainType.Forest ? mission.ForestCost : mission.GroundCost;
        }

        /// <summary>The encounter at a position that still matters: towns always, other sites until resolved.</summary>
        public EncounterDefinition Site(CampaignState campaign, WorldPosition position)
        {
            if (!sites.TryGetValue(position, out EncounterDefinition encounter))
            {
                return null;
            }

            return encounter.Kind == EncounterKind.Town || !campaign.IsCompleted(encounter.Id) ? encounter : null;
        }

        /// <summary>One step to a neighbouring cell; diagonals may not cut a blocked corner.</summary>
        public bool CanStep(WorldPosition from, WorldPosition to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (from == to || Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || !IsWalkable(to))
            {
                return false;
            }

            if (dx == 0 || dy == 0)
            {
                return true;
            }

            return IsWalkable(new WorldPosition(from.X + dx, from.Y)) && IsWalkable(new WorldPosition(from.X, from.Y + dy));
        }

        /// <summary>
        /// Cheapest route from the hero to <paramref name="destination"/>, excluding the start. Ties are broken by row,
        /// then column, so the same request always returns the same route. Unresolved sites end a route but are not crossed.
        /// </summary>
        public IReadOnlyList<WorldPosition> FindRoute(CampaignState campaign, WorldPosition destination)
        {
            List<WorldPosition> route = new List<WorldPosition>();
            WorldPosition from = campaign.Position;
            if (!IsWalkable(destination) || from == destination)
            {
                return route.AsReadOnly();
            }

            List<WorldPosition> open = new List<WorldPosition> { from };
            Dictionary<WorldPosition, int> costs = new Dictionary<WorldPosition, int> { [from] = 0 };
            Dictionary<WorldPosition, WorldPosition> previous = new Dictionary<WorldPosition, WorldPosition>();
            while (open.Count > 0)
            {
                WorldPosition current = TakeCheapest(open, costs);
                if (current == destination)
                {
                    break;
                }

                if (current != from && Site(campaign, current) != null)
                {
                    continue;
                }

                Relax(current, open, costs, previous);
            }

            if (!previous.ContainsKey(destination))
            {
                return route.AsReadOnly();
            }

            for (WorldPosition step = destination; step != from; step = previous[step])
            {
                route.Add(step);
            }

            route.Reverse();
            return route.AsReadOnly();
        }

        private static WorldPosition TakeCheapest(List<WorldPosition> open, Dictionary<WorldPosition, int> costs)
        {
            int bestIndex = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (IsCheaper(open[i], open[bestIndex], costs))
                {
                    bestIndex = i;
                }
            }

            WorldPosition best = open[bestIndex];
            open.RemoveAt(bestIndex);
            return best;
        }

        private static bool IsCheaper(WorldPosition candidate, WorldPosition best, Dictionary<WorldPosition, int> costs)
        {
            int candidateCost = costs[candidate];
            int bestCost = costs[best];
            if (candidateCost != bestCost)
            {
                return candidateCost < bestCost;
            }

            return candidate.Y != best.Y ? candidate.Y < best.Y : candidate.X < best.X;
        }

        private void Relax(WorldPosition current, List<WorldPosition> open, Dictionary<WorldPosition, int> costs,
            Dictionary<WorldPosition, WorldPosition> previous)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    WorldPosition next = new WorldPosition(current.X + dx, current.Y + dy);
                    if (!CanStep(current, next))
                    {
                        continue;
                    }

                    int candidate = costs[current] + Cost(next);
                    if (costs.TryGetValue(next, out int known) && candidate >= known)
                    {
                        continue;
                    }

                    costs[next] = candidate;
                    previous[next] = current;
                    if (!open.Contains(next))
                    {
                        open.Add(next);
                    }
                }
            }
        }
    }
}
