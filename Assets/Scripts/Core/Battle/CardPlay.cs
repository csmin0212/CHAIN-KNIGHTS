namespace ChainKnights.Core.Battle
{
    /// <summary>카드 효과의 대상. 단일 대상 카드에만 쓰이고, 자신·전체 대상 카드는 무시한다.</summary>
    public readonly struct TargetRef
    {
        public BattleSide Side { get; }
        public int Index { get; }

        /// <summary>보스의 부위 번호. -1이면 본체.</summary>
        public int Part { get; }

        public TargetRef(BattleSide side, int index, int part = -1)
        {
            Side = side;
            Index = index;
            Part = part;
        }

        public static TargetRef Enemy(int index, int part = -1) => new TargetRef(BattleSide.Enemy, index, part);

        public static TargetRef Ally(int index) => new TargetRef(BattleSide.Ally, index);

        public static readonly TargetRef None = new TargetRef(BattleSide.Enemy, -1);
    }

    /// <summary>플레이어가 이번 턴에 내기로 고른 카드 한 장.</summary>
    public sealed class CardPlay
    {
        public int InstanceId { get; }
        public TargetRef Target { get; }

        public CardPlay(int instanceId, TargetRef target)
        {
            InstanceId = instanceId;
            Target = target;
        }

        public CardPlay(int instanceId)
            : this(instanceId, TargetRef.None)
        {
        }
    }

    public sealed class SubmitResult
    {
        public bool Success { get; }
        public string Error { get; }

        private SubmitResult(bool success, string error)
        {
            Success = success;
            Error = error;
        }

        public static readonly SubmitResult Ok = new SubmitResult(true, null);

        public static SubmitResult Fail(string error) => new SubmitResult(false, error);
    }
}
