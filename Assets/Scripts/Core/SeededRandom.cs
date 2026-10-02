using System;

namespace TwentyTons.Core
{
    /// <summary>
    /// A random source with a seed, so a simulation run can be replayed exactly. Unity's
    /// UnityEngine.Random is global and shared with the engine, which makes replays impossible;
    /// this wraps System.Random instead and is handed around explicitly.
    /// </summary>
    public sealed class SeededRandom
    {
        private readonly Random _random;

        public SeededRandom(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>A float in [0, 1).</summary>
        public float Value => (float)_random.NextDouble();

        /// <summary>A float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * Value;

        /// <summary>An int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive) => _random.Next(min, maxExclusive);

        /// <summary>True with the given probability (0..1).</summary>
        public bool Chance(float probability) => Value < probability;
    }
}
