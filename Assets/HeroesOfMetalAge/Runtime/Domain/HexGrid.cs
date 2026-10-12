using System;
using System.Collections.Generic;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Offset-coordinate hex geometry (odd rows shifted right).</summary>
    public static class HexGrid
    {
        public static int Distance(HexPosition a, HexPosition b)
        {
            int ax = a.X - (a.Y - (a.Y & 1)) / 2;
            int bx = b.X - (b.Y - (b.Y & 1)) / 2;
            return (Math.Abs(ax - bx) + Math.Abs(a.Y - b.Y) + Math.Abs(-ax - a.Y + bx + b.Y)) / 2;
        }

        public static IEnumerable<HexPosition> Neighbors(HexPosition position, int width, int height)
        {
            int shift = position.Y % 2 == 0 ? -1 : 0;
            HexPosition[] cells =
            {
                new HexPosition(position.X - 1, position.Y),
                new HexPosition(position.X + 1, position.Y),
                new HexPosition(position.X + shift, position.Y - 1),
                new HexPosition(position.X + shift + 1, position.Y - 1),
                new HexPosition(position.X + shift, position.Y + 1),
                new HexPosition(position.X + shift + 1, position.Y + 1)
            };

            foreach (HexPosition cell in cells)
            {
                if (cell.X >= 0 && cell.Y >= 0 && cell.X < width && cell.Y < height)
                {
                    yield return cell;
                }
            }
        }
    }
}
