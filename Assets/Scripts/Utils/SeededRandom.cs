using System;

namespace IdlePowerGen.Utils
{
    // Deterministic RNG. §8.2: one instance per concern (weather, faults, market)
    // to avoid coupling between systems.
    public class SeededRandom
    {
        private Random _rng;
        private readonly int _seed;

        public SeededRandom(int seed)
        {
            _seed = seed;
            _rng  = new Random(seed);
        }

        // Restores the stream to its initial state (e.g. for offline catch-up replays)
        public void Reset() => _rng = new Random(_seed);

        public double NextDouble()                            => _rng.NextDouble();
        public double NextDouble(double min, double max)      => min + _rng.NextDouble() * (max - min);
        public int    NextInt(int minInclusive, int maxExcl)  => _rng.Next(minInclusive, maxExcl);
        public bool   NextBool(double probability)            => _rng.NextDouble() < probability;
    }
}
