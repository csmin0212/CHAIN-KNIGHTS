using System;

namespace ChainKnights.Core.Battle
{
    /// <summary>전투의 모든 무작위 결정이 지나가는 곳. 서버가 시드를 정하면 결과가 재현된다.</summary>
    public interface IBattleRandom
    {
        /// <summary>0 이상 1 미만.</summary>
        double NextDouble();

        /// <summary>0 이상 maxExclusive 미만.</summary>
        int Next(int maxExclusive);
    }

    public sealed class SeededBattleRandom : IBattleRandom
    {
        private readonly Random random;

        public SeededBattleRandom(int seed)
        {
            random = new Random(seed);
        }

        public double NextDouble() => random.NextDouble();

        public int Next(int maxExclusive) => random.Next(maxExclusive);
    }
}
