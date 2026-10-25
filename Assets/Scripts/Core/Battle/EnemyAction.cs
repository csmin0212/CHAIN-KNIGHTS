using System;
using System.Collections.Generic;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    public enum EnemyActionKind
    {
        /// <summary>아서를 공격한다.</summary>
        Attack,
        /// <summary>적 자신(또는 적 전체)의 주는 피해를 올린다.</summary>
        PowerUp,
    }

    /// <summary>적 행동 하나의 정의. 어느 부위에서 나오는지(SourcePart)가 예고 표시와 취소 판정에 쓰인다.</summary>
    public sealed class EnemyAction
    {
        public string Name { get; }
        public EnemyActionKind Kind { get; }

        /// <summary>행동이 나오는 부위 번호. -1이면 본체.</summary>
        public int SourcePart { get; }
        public StatType Stat { get; }
        public int Power { get; }

        /// <summary>공격이면 아서 전원, 강화면 적 전원이 대상.</summary>
        public bool TargetsAll { get; }
        public int Turns { get; }

        private EnemyAction(string name, EnemyActionKind kind, int sourcePart, StatType stat, int power, bool targetsAll, int turns)
        {
            Name = name;
            Kind = kind;
            SourcePart = sourcePart;
            Stat = stat;
            Power = power;
            TargetsAll = targetsAll;
            Turns = turns;
        }

        public static EnemyAction Attack(string name, StatType stat, int power, bool targetsAll = false, int sourcePart = -1)
        {
            return new EnemyAction(name, EnemyActionKind.Attack, sourcePart, stat, power, targetsAll, 0);
        }

        public static EnemyAction PowerUp(string name, StatType stat, int amount, int turns, bool allEnemies = false, int sourcePart = -1)
        {
            return new EnemyAction(name, EnemyActionKind.PowerUp, sourcePart, stat, amount, allEnemies, turns);
        }
    }

    /// <summary>적이 매 턴 무엇을 할지 정한다. 대상 아서는 전투가 고른다.</summary>
    public interface IEnemyBrain
    {
        IReadOnlyList<EnemyAction> Decide(Enemy self, int turn, IBattleRandom random);
    }

    /// <summary>턴 번호에 따라 정해진 행동을 하는 적. 원작 보스는 대부분 이렇게 턴 패턴이 정해져 있다.</summary>
    public sealed class PatternBrain : IEnemyBrain
    {
        private readonly Func<int, IReadOnlyList<EnemyAction>> pattern;

        public PatternBrain(Func<int, IReadOnlyList<EnemyAction>> pattern)
        {
            this.pattern = pattern;
        }

        public IReadOnlyList<EnemyAction> Decide(Enemy self, int turn, IBattleRandom random) => pattern(turn);
    }
}
