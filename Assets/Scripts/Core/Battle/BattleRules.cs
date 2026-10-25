using System;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>전투 규칙 상수. 원작 출시판 기준 (기획서 2장).</summary>
    public static class BattleRules
    {
        public const int MaxArthurs = 4;
        public const int DeckSize = 10;
        public const int HandSize = 5;

        /// <summary>코스트는 3에서 시작해 매 턴 +1, 최대 10. 남은 코스트는 이월되지 않는다.</summary>
        public const int StartCost = 3;
        public const int MaxCost = 10;

        public const double BaseCritChance = 0.15;
        public const double CritMultiplier = 1.5;

        public static int CostForTurn(int turn)
        {
            return Math.Min(StartCost + turn - 1, MaxCost);
        }

        /// <summary>체인 배율: 2체인 ×1.2, 3체인 ×1.4, 4체인 ×1.6. 피해와 회복에 곱한다.</summary>
        public static double ChainMultiplier(int chain)
        {
            return chain <= 1 ? 1.0 : 1.0 + 0.2 * (Math.Min(chain, MaxArthurs) - 1);
        }

        /// <summary>체인 보너스: 고정 수치 버프·디버프에 2체인 +20, 3체인 +40, 4체인 +60.</summary>
        public static int ChainFlatBonus(int chain)
        {
            return chain <= 1 ? 0 : 20 * (Math.Min(chain, MaxArthurs) - 1);
        }

        /// <summary>
        /// 같은 턴의 처리 순서: 지원 → 방어 → 치유 → 방해 → 공격.
        /// 그래서 공격보다 늦게 고른 버프도 그 턴의 공격에 들어간다.
        /// </summary>
        public static int ResolvePriority(SkillCategory category)
        {
            switch (category)
            {
                case SkillCategory.Support: return 0;
                case SkillCategory.Defense: return 1;
                case SkillCategory.Heal: return 2;
                case SkillCategory.Hinder: return 3;
                default: return 4;
            }
        }
    }

    /// <summary>속성 상성. 화 → 풍 → 빙 → 화 순환, 광 ⇄ 암은 서로 강하다.</summary>
    public static class ElementChart
    {
        public const double Advantage = 2.0;
        public const double Disadvantage = 0.5;

        public static double Multiplier(Element attack, Element defense)
        {
            if (IsLightDarkPair(attack, defense) || Beats(attack, defense))
                return Advantage;
            if (Beats(defense, attack))
                return Disadvantage;
            return 1.0;
        }

        private static bool IsLightDarkPair(Element a, Element b)
        {
            return (a == Element.Light && b == Element.Dark) || (a == Element.Dark && b == Element.Light);
        }

        private static bool Beats(Element a, Element b)
        {
            return (a == Element.Fire && b == Element.Wind)
                || (a == Element.Wind && b == Element.Ice)
                || (a == Element.Ice && b == Element.Fire);
        }
    }
}
