using System;
using System.Collections.Generic;
using System.Linq;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Traversal model of the adventure map: terrain costs, blocking cells, encounter sites and routing.</summary>
    public sealed class WorldMap
    {
        // map distance of a step, used only to prefer natural routes: a diagonal is about √2 times a straight step
        private const int STRAIGHT_LENGTH = 10;
        private const int DIAGONAL_LENGTH = 14;

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
            return terrain != TerrainType.Mountain && terrain != TerrainType.Water && terrain != TerrainType.Blocked;
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
        /// Cheapest route from the hero to <paramref name="destination"/>, excluding the start. Among equally cheap routes the
        /// shortest on the map wins (a diagonal step is longer than a straight one), then the one closest to the straight line
        /// to the destination; remaining ties are broken by row, then column, so the same request always returns the same
        /// route. Unresolved sites end a route but are not crossed.
        /// </summary>
        public IReadOnlyList<WorldPosition> FindRoute(CampaignState campaign, WorldPosition destination)
        {
            List<WorldPosition> route = new List<WorldPosition>();
            WorldPosition from = campaign.Position;
            if (!IsWalkable(destination) || from == destination)
            {
                return route.AsReadOnly();
            }

            OpenSet open = new OpenSet();
            open.Push(from, default);
            Dictionary<WorldPosition, RouteCost> costs = new Dictionary<WorldPosition, RouteCost> { [from] = default };
            Dictionary<WorldPosition, WorldPosition> previous = new Dictionary<WorldPosition, WorldPosition>();
            while (open.Count > 0)
            {
                OpenEntry entry = open.Pop();
                WorldPosition current = entry.Position;
                // a cell pushed again with a lower cost leaves its older, dearer entry behind
                if (entry.Cost.CompareTo(costs[current]) != 0)
                {
                    continue;
                }

                if (current == destination)
                {
                    break;
                }

                if (current != from && Site(campaign, current) != null)
                {
                    continue;
                }

                Relax(current, from, destination, open, costs, previous);
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

        private void Relax(WorldPosition current, WorldPosition from, WorldPosition destination, OpenSet open,
            Dictionary<WorldPosition, RouteCost> costs, Dictionary<WorldPosition, WorldPosition> previous)
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

                    int length = dx != 0 && dy != 0 ? DIAGONAL_LENGTH : STRAIGHT_LENGTH;
                    RouteCost candidate = costs[current].Add(Cost(next), length, Deviation(next, from, destination));
                    if (costs.TryGetValue(next, out RouteCost known) && candidate.CompareTo(known) >= 0)
                    {
                        continue;
                    }

                    costs[next] = candidate;
                    previous[next] = current;
                    open.Push(next, candidate);
                }
            }
        }

        // how far a cell lies from the straight line to the destination, scaled by that line's length
        private static long Deviation(WorldPosition cell, WorldPosition from, WorldPosition destination)
        {
            long lineX = destination.X - from.X;
            long lineY = destination.Y - from.Y;
            return Math.Abs(lineX * (cell.Y - from.Y) - lineY * (cell.X - from.X));
        }

        /// <summary>
        /// What a route has cost so far, compared in order: movement points (the rule), distance on the map, then distance from
        /// the straight line. Only movement points are a game rule; the rest picks the natural route among equally cheap ones.
        /// </summary>
        private readonly struct RouteCost
        {
            public RouteCost(int movement, int length, long deviation)
            {
                Movement = movement;
                Length = length;
                Deviation = deviation;
            }

            public int Movement { get; }
            public int Length { get; }
            public long Deviation { get; }

            public RouteCost Add(int movement, int length, long deviation)
            {
                return new RouteCost(Movement + movement, Length + length, Deviation + deviation);
            }

            public int CompareTo(RouteCost other)
            {
                if (Movement != other.Movement)
                {
                    return Movement.CompareTo(other.Movement);
                }

                return Length != other.Length ? Length.CompareTo(other.Length) : Deviation.CompareTo(other.Deviation);
            }
        }

        private readonly struct OpenEntry
        {
            public OpenEntry(WorldPosition position, RouteCost cost)
            {
                Position = position;
                Cost = cost;
            }

            public WorldPosition Position { get; }
            public RouteCost Cost { get; }

            /// <summary>Cheapest first, then by row, then column: the tie-breaking that keeps routes deterministic.</summary>
            public bool IsBefore(OpenEntry other)
            {
                int comparison = Cost.CompareTo(other.Cost);
                if (comparison != 0)
                {
                    return comparison < 0;
                }

                return Position.Y != other.Position.Y ? Position.Y < other.Position.Y : Position.X < other.Position.X;
            }
        }

        /// <summary>Binary min-heap of open cells, so routing stays fast on large maps.</summary>
        private sealed class OpenSet
        {
            private readonly List<OpenEntry> heap = new List<OpenEntry>();

            public int Count => heap.Count;

            public void Push(WorldPosition position, RouteCost cost)
            {
                heap.Add(new OpenEntry(position, cost));
                int child = heap.Count - 1;
                while (child > 0)
                {
                    int parent = (child - 1) / 2;
                    if (!heap[child].IsBefore(heap[parent]))
                    {
                        break;
                    }

                    Swap(child, parent);
                    child = parent;
                }
            }

            public OpenEntry Pop()
            {
                OpenEntry first = heap[0];
                int last = heap.Count - 1;
                heap[0] = heap[last];
                heap.RemoveAt(last);
                int parent = 0;
                while (true)
                {
                    int left = parent * 2 + 1;
                    int right = left + 1;
                    int smallest = parent;
                    if (left < heap.Count && heap[left].IsBefore(heap[smallest]))
                    {
                        smallest = left;
                    }

                    if (right < heap.Count && heap[right].IsBefore(heap[smallest]))
                    {
                        smallest = right;
                    }

                    if (smallest == parent)
                    {
                        return first;
                    }

                    Swap(parent, smallest);
                    parent = smallest;
                }
            }

            private void Swap(int a, int b)
            {
                OpenEntry value = heap[a];
                heap[a] = heap[b];
                heap[b] = value;
            }
        }
    }
}
