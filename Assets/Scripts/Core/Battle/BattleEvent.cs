using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    public enum BattleOutcome
    {
        Ongoing,
        Victory,
        Defeat,
    }

    public enum BattleEventType
    {
        TurnStarted,
        Chain,
        CardPlayed,
        Damage,
        Heal,
        StatusApplied,
        DrawQueued,
        Stunned,
        PartDestroyed,
        EnemyDefeated,
        ArthurDown,
        EnemyAction,
        EnemyActionCanceled,
        PoisonTick,
        HealTick,
        BattleEnded,
    }

    /// <summary>
    /// 전투에서 일어난 일 하나. 서버는 이 목록을 모든 클라이언트에 보내고,
    /// 클라이언트는 같은 순서로 연출만 한다.
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventType Type { get; set; }
        public BattleSide ActorSide { get; set; }
        public int ActorIndex { get; set; } = -1;
        public BattleSide TargetSide { get; set; }
        public int TargetIndex { get; set; } = -1;
        public int PartIndex { get; set; } = -1;
        public int CardId { get; set; }
        public EffectKind EffectKind { get; set; }
        public Element Element { get; set; }
        public int Value { get; set; }
        public int Chain { get; set; }
        public bool IsCritical { get; set; }
        public bool IsAwakened { get; set; }
        public BattleOutcome Outcome { get; set; }

        /// <summary>디버그와 로그용 한 줄 설명.</summary>
        public string Message { get; set; }

        public override string ToString() => $"[{Type}] {Message}";
    }
}
