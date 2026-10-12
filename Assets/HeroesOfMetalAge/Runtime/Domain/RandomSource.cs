using System;

namespace HeroesOfMetalAge.Domain
{
    /// <summary>Randomness for gameplay rules; injected so battles can be replayed with a fixed seed.</summary>
    public interface IRandomSource
    {
        /// <summary>A value in [0, 1).</summary>
        double NextValue();
    }

    public sealed class SeededRandomSource : IRandomSource
    {
        private readonly Random random;

        public SeededRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public double NextValue()
        {
            return random.NextDouble();
        }
    }
}
