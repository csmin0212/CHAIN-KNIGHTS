using System.Collections.Generic;
using System.Linq;

namespace ChainKnights.Core.Cards
{
    /// <summary>카드와 효과를 화면에 보여줄 한국어 문구.</summary>
    public static class CardText
    {
        public static string JobName(Job job)
        {
            switch (job)
            {
                case Job.Warrior: return "전사";
                case Job.Guardian: return "수호자";
                case Job.Mage: return "마법사";
                case Job.Priest: return "사제";
                default: return "공용";
            }
        }

        public static string ElementName(Element element)
        {
            switch (element)
            {
                case Element.Fire: return "화";
                case Element.Ice: return "빙";
                case Element.Wind: return "풍";
                case Element.Light: return "광";
                default: return "암";
            }
        }

        public static string Stars(int rarity) => new string('★', rarity);

        public static string Describe(SkillEffect effect, int value)
        {
            string target = TargetName(effect.Target);
            string stat = StatName(effect.Stat);
            string turns = effect.Turns > 0 ? $"{effect.Turns}턴 " : "";
            switch (effect.Kind)
            {
                case EffectKind.Damage: return $"{target} {stat} {value}";
                case EffectKind.Heal: return $"{target} 회복 {value}";
                case EffectKind.HealOverTime: return $"{target} {turns}매 턴 회복 {value}";
                case EffectKind.DamageUp: return $"{target} {turns}{stat} 피해 +{value}";
                case EffectKind.DefenseUp: return $"{target} {turns}{stat} 방어 +{value}";
                case EffectKind.DamageDown: return $"{target} {turns}{stat} 피해 −{value}";
                case EffectKind.DefenseDown: return $"{target} {turns}{stat} 방어 −{value}";
                case EffectKind.Poison: return $"{target} {turns}독 {value}";
                case EffectKind.Draw: return $"드로우 +{effect.BaseValue}";
                case EffectKind.CritRateUp: return $"크리티컬 {effect.Chance:P0}";
                case EffectKind.Stun: return $"{effect.Chance:P0} 행동 정지";
                case EffectKind.Lifesteal: return $"흡혈 {effect.Chance:P0}";
                case EffectKind.HealBoost: return $"{turns}회복량 +{effect.Chance:P0}";
                default: return effect.Kind.ToString();
            }
        }

        public static IEnumerable<string> DescribeSkill(CardSkill skill, int level, int maxLevel)
        {
            return skill.Effects.Select(e => Describe(e, e.ValueAt(level, maxLevel)));
        }

        private static string TargetName(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.Self: return "자신";
                case EffectTarget.AllySingle: return "아군 1명";
                case EffectTarget.AllAllies: return "아군 전원";
                case EffectTarget.EnemySingle: return "적 단일";
                default: return "적 전체";
            }
        }

        private static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Physical: return "물리";
                case StatType.Magic: return "마법";
                case StatType.All: return "전체";
                default: return "";
            }
        }
    }
}
