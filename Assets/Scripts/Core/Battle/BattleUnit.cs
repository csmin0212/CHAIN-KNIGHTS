using System;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    public enum BattleSide
    {
        Ally,
        Enemy,
    }

    /// <summary>아서와 적의 공통 부분: HP, 방어, 지속 효과.</summary>
    public abstract class BattleUnit
    {
        private readonly List<StatusEffect> statuses = new List<StatusEffect>();

        public abstract BattleSide Side { get; }
        public int Index { get; }
        public string Name { get; }
        public int MaxHp { get; }
        public int Hp { get; private set; }
        public bool IsAlive => Hp > 0;
        public int BasePhysicalDefense { get; }
        public int BaseMagicDefense { get; }
        public IReadOnlyList<StatusEffect> Statuses => statuses;

        protected BattleUnit(int index, string name, int maxHp, int physicalDefense, int magicDefense)
        {
            Index = index;
            Name = name;
            MaxHp = maxHp;
            Hp = maxHp;
            BasePhysicalDefense = physicalDefense;
            BaseMagicDefense = magicDefense;
        }

        /// <summary>걸려 있는 같은 종류 효과의 고정 수치 합. 전체(All) 효과는 물리와 마법 양쪽에 들어간다.</summary>
        public int StatusTotal(EffectKind kind, StatType stat)
        {
            return statuses.Where(s => s.Matches(kind, stat)).Sum(s => s.Value);
        }

        /// <summary>방어 디버프는 기본 방어보다 클 수 있다. 음수 방어는 받는 피해를 늘린다.</summary>
        public int Defense(StatType stat)
        {
            int baseDefense = stat == StatType.Magic ? BaseMagicDefense : BasePhysicalDefense;
            return baseDefense + StatusTotal(EffectKind.DefenseUp, stat) - StatusTotal(EffectKind.DefenseDown, stat);
        }

        public double HealBoostRatio => statuses.Where(s => s.Kind == EffectKind.HealBoost).Sum(s => s.Ratio);

        public void AddStatus(StatusEffect status)
        {
            statuses.Add(status);
        }

        /// <summary>실제로 깎인 HP를 돌려준다.</summary>
        public int TakeDamage(int amount)
        {
            int dealt = Math.Min(Math.Max(amount, 0), Hp);
            Hp -= dealt;
            return dealt;
        }

        /// <summary>실제로 회복된 HP를 돌려준다. 쓰러진 유닛은 회복하지 않는다.</summary>
        public int Heal(int amount)
        {
            if (!IsAlive)
                return 0;
            int healed = Math.Min(Math.Max(amount, 0), MaxHp - Hp);
            Hp += healed;
            return healed;
        }

        internal void TickStatuses()
        {
            statuses.RemoveAll(s => s.Tick());
        }
    }
}
