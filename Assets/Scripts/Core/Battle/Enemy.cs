using System.Collections.Generic;
using System.Linq;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>
    /// 보스의 부위. 부위에 준 피해는 본체에도 그대로 들어간다.
    /// 부위가 파괴되면 그 부위에서 나오는 적 행동이 취소된다.
    /// </summary>
    public sealed class EnemyPart
    {
        private readonly IReadOnlyDictionary<Element, double> elementMultipliers;

        public int Index { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsDestroyed => Hp <= 0;

        internal EnemyPart(int index, PartSetup setup)
        {
            Index = index;
            Name = setup.Name;
            MaxHp = setup.MaxHp;
            Hp = setup.MaxHp;
            elementMultipliers = setup.ElementMultipliers;
        }

        /// <summary>부위마다 속성 배율이 따로 정해져 있으면 그 값, 아니면 본체 속성의 상성을 쓴다.</summary>
        internal double ElementMultiplier(Element attack, Element bodyElement)
        {
            if (elementMultipliers != null && elementMultipliers.TryGetValue(attack, out var multiplier))
                return multiplier;
            return ElementChart.Multiplier(attack, bodyElement);
        }

        /// <summary>이번 피해로 파괴되었으면 true.</summary>
        internal bool TakeDamage(int amount)
        {
            if (IsDestroyed)
                return false;
            Hp = System.Math.Max(Hp - amount, 0);
            return IsDestroyed;
        }
    }

    /// <summary>적이 이번 턴에 할 행동. 턴 시작에 예고되고, 플레이어가 카드를 낸 뒤 실행된다.</summary>
    public sealed class EnemyIntent
    {
        public EnemyAction Action { get; }

        /// <summary>단일 공격이면 노리는 아서의 번호. 아니면 -1.</summary>
        public int TargetArthurIndex { get; }

        internal EnemyIntent(EnemyAction action, int targetArthurIndex)
        {
            Action = action;
            TargetArthurIndex = targetArthurIndex;
        }
    }

    public sealed class Enemy : BattleUnit
    {
        private readonly List<EnemyIntent> intents = new List<EnemyIntent>();

        public override BattleSide Side => BattleSide.Enemy;
        public Element Element { get; }
        public IReadOnlyList<EnemyPart> Parts { get; }
        public IEnemyBrain Brain { get; }

        /// <summary>이번 턴 예고된 행동.</summary>
        public IReadOnlyList<EnemyIntent> Intents => intents;

        /// <summary>남은 행동 정지 턴. 0보다 크면 이번 턴 행동을 하지 않는다.</summary>
        public int StunTurns { get; internal set; }

        internal Enemy(int index, EnemySetup setup)
            : base(index, setup.Name, setup.MaxHp, setup.PhysicalDefense, setup.MagicDefense)
        {
            Element = setup.Element;
            Brain = setup.Brain;
            Parts = setup.Parts.Select((p, i) => new EnemyPart(i, p)).ToList();
        }

        public EnemyPart Part(int partIndex)
        {
            return partIndex >= 0 && partIndex < Parts.Count ? Parts[partIndex] : null;
        }

        internal void SetIntents(IEnumerable<EnemyIntent> next)
        {
            intents.Clear();
            intents.AddRange(next);
        }
    }
}
