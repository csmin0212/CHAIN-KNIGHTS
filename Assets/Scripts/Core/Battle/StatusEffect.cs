using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>
    /// 유닛에 걸린 지속 효과 (버프, 디버프, 독, 지속 회복, 회복량 증가).
    /// 턴이 끝날 때 TurnsLeft가 1씩 줄고, 0이 되면 사라진다. 그래서 1턴 버프는 건 그 턴에만 유효하다.
    /// </summary>
    public sealed class StatusEffect
    {
        public EffectKind Kind { get; }
        public StatType Stat { get; }
        public int Value { get; }

        /// <summary>회복량 증가처럼 비율로 동작하는 효과의 비율.</summary>
        public double Ratio { get; }
        public int TurnsLeft { get; private set; }

        public StatusEffect(EffectKind kind, StatType stat, int value, int turns, double ratio = 0)
        {
            Kind = kind;
            Stat = stat;
            Value = value;
            TurnsLeft = turns;
            Ratio = ratio;
        }

        public bool Matches(EffectKind kind, StatType stat)
        {
            return Kind == kind && (Stat == stat || Stat == StatType.All);
        }

        /// <summary>남은 턴을 하나 줄이고, 효과가 끝났으면 true.</summary>
        public bool Tick()
        {
            TurnsLeft--;
            return TurnsLeft <= 0;
        }
    }
}
