using System;

namespace ChainKnights.Core.Cards
{
    /// <summary>스킬 하나를 이루는 효과 하나. 카드 데이터는 이 효과들의 조합이다.</summary>
    public sealed class SkillEffect
    {
        public EffectKind Kind { get; }
        public EffectTarget Target { get; }
        public StatType Stat { get; }

        /// <summary>레벨 1일 때의 수치.</summary>
        public int BaseValue { get; }

        /// <summary>최대 레벨일 때의 수치.</summary>
        public int MaxValue { get; }

        /// <summary>지속 턴 수. 0이면 즉시 효과.</summary>
        public int Turns { get; }

        /// <summary>확률 또는 비율 (크리티컬, 행동 정지, 흡혈, 회복량 증가). 쓰지 않으면 0.</summary>
        public float Chance { get; }

        /// <summary>원작 자료에 수치가 없어 변환기가 추정한 값이면 true.</summary>
        public bool IsEstimated { get; }

        public SkillEffect(
            EffectKind kind,
            EffectTarget target,
            StatType stat = StatType.None,
            int baseValue = 0,
            int maxValue = 0,
            int turns = 0,
            float chance = 0f,
            bool isEstimated = false)
        {
            Kind = kind;
            Target = target;
            Stat = stat;
            BaseValue = baseValue;
            MaxValue = maxValue;
            Turns = turns;
            Chance = chance;
            IsEstimated = isEstimated;
        }

        /// <summary>카드 레벨에 맞는 수치. 레벨 1과 최대 레벨 사이를 선형으로 나눈다.</summary>
        public int ValueAt(int level, int maxLevel)
        {
            if (maxLevel <= 1 || level >= maxLevel)
                return MaxValue;
            if (level <= 1)
                return BaseValue;

            double t = (level - 1) / (double)(maxLevel - 1);
            return BaseValue + (int)Math.Round((MaxValue - BaseValue) * t, MidpointRounding.AwayFromZero);
        }
    }
}
