using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>
    /// 전투 중에 아군에게 보낼 수 있는 정해진 신호. 자유 채팅은 없다.
    /// 무엇을 할지 "의도"만 전하고, 어떤 카드를 냈는지는 알려 주지 않는다 (보이지 않는 협동).
    /// </summary>
    public enum SignalKind
    {
        /// <summary>공격 갈게</summary>
        Attack,
        /// <summary>방어 부탁</summary>
        NeedDefense,
        /// <summary>회복 부탁</summary>
        NeedHeal,
        /// <summary>버프 줄게</summary>
        Buff,
        /// <summary>부위 노리자 (부위 번호를 함께 보낸다)</summary>
        FocusPart,
        /// <summary>○ 체인 가자 (속성을 함께 보낸다)</summary>
        Chain,
    }

    public sealed class BattleSignal
    {
        public int ArthurIndex { get; }
        public SignalKind Kind { get; }

        /// <summary>Chain 신호의 속성. 다른 신호는 null.</summary>
        public Element? Element { get; }

        /// <summary>FocusPart 신호의 부위 번호. 다른 신호는 -1.</summary>
        public int PartIndex { get; }

        public BattleSignal(int arthurIndex, SignalKind kind, Element? element = null, int partIndex = -1)
        {
            ArthurIndex = arthurIndex;
            Kind = kind;
            Element = element;
            PartIndex = partIndex;
        }
    }
}
