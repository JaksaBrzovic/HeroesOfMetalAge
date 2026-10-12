using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Cell on the adventure map grid.</summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public WorldPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public static bool operator ==(WorldPosition a, WorldPosition b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(WorldPosition a, WorldPosition b)
        {
            return !a.Equals(b);
        }

        public bool Equals(WorldPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is WorldPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((X * 397) ^ Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }

    /// <summary>Cell on the tactical combat board (offset coordinates, odd rows shifted right).</summary>
    public readonly struct HexPosition : IEquatable<HexPosition>
    {
        public HexPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public static bool operator ==(HexPosition a, HexPosition b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(HexPosition a, HexPosition b)
        {
            return !a.Equals(b);
        }

        public bool Equals(HexPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is HexPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((X * 397) ^ Y);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }
}
