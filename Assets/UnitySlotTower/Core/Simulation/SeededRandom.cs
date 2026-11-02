namespace UnitySlotTower.Core.Simulation
{
    /// <summary>コアが使う乱数。テストではシード固定の実装を注入して結果を再現する。</summary>
    public interface IRandom
    {
        /// <summary>[minInclusive, maxExclusive) の整数。</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>[0, 1) の実数。</summary>
        double NextDouble();
    }

    /// <summary>SplitMix64。プラットフォームや .NET の実装に依存せず、同じシードで同じ列を返す。</summary>
    public sealed class SeededRandom : IRandom
    {
        private ulong _state;

        public SeededRandom(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new System.ArgumentOutOfRangeException(nameof(maxExclusive));
            }

            var range = (ulong)((long)maxExclusive - minInclusive);
            // 偏りを避けるため、range で割り切れない端数の領域は引き直す。
            var limit = ulong.MaxValue - ulong.MaxValue % range;
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value >= limit);

            return (int)((long)minInclusive + (long)(value % range));
        }

        public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));
    }
}
