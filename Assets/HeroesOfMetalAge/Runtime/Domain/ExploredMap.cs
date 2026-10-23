using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>
    /// Fog of war: the adventure map cells the hero has seen. As in classic strategy games a cell, once seen, stays explored
    /// for the rest of the campaign; routes never lead into unexplored land.
    /// </summary>
    public sealed class ExploredMap
    {
        private readonly bool[] cells;

        internal ExploredMap(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new bool[width * height];
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>Explored cells; it only grows, so a view can redraw when it changes.</summary>
        public int Count { get; private set; }
        public bool IsComplete => Count == cells.Length;

        public bool IsExplored(WorldPosition position)
        {
            return position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height && cells[position.Y * Width + position.X];
        }

        /// <summary>Explores the round area of cells whose centres lie within <paramref name="radius"/> cells of the centre.</summary>
        internal void Reveal(WorldPosition center, int radius)
        {
            // half a cell more than the radius keeps the edge round instead of leaving single cells sticking out
            int limit = radius * radius + radius;
            for (int y = Math.Max(0, center.Y - radius); y <= Math.Min(Height - 1, center.Y + radius); y++)
            {
                for (int x = Math.Max(0, center.X - radius); x <= Math.Min(Width - 1, center.X + radius); x++)
                {
                    int dx = x - center.X;
                    int dy = y - center.Y;
                    if (dx * dx + dy * dy <= limit)
                    {
                        Explore(y * Width + x);
                    }
                }
            }
        }

        internal void RevealAll()
        {
            for (int i = 0; i < cells.Length; i++)
            {
                Explore(i);
            }
        }

        /// <summary>The explored cells as packed bits (row by row from the top-left, low bit first) in Base64.</summary>
        internal string Encode()
        {
            byte[] bytes = new byte[(cells.Length + 7) / 8];
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i])
                {
                    bytes[i / 8] |= (byte)(1 << (i % 8));
                }
            }

            return Convert.ToBase64String(bytes);
        }

        /// <summary>Reads what <see cref="Encode"/> wrote for a map of the same size.</summary>
        /// <exception cref="ArgumentException">The text is not Base64 or does not fit the map.</exception>
        internal static ExploredMap Decode(string encoded, int width, int height)
        {
            ExploredMap map = new ExploredMap(width, height);
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid explored map.");
            }

            if (bytes.Length != (map.cells.Length + 7) / 8)
            {
                throw new ArgumentException("Invalid explored map size.");
            }

            for (int i = 0; i < map.cells.Length; i++)
            {
                if ((bytes[i / 8] & (1 << (i % 8))) != 0)
                {
                    map.Explore(i);
                }
            }

            return map;
        }

        private void Explore(int index)
        {
            if (!cells[index])
            {
                cells[index] = true;
                Count++;
            }
        }
    }
}
