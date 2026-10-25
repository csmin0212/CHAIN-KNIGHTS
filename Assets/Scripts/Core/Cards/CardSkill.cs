using System.Collections.Generic;

namespace ChainKnights.Core.Cards
{
    /// <summary>카드 한 장이 쓰는 스킬 (통상 스킬 또는 각성 스킬).</summary>
    public sealed class CardSkill
    {
        public IReadOnlyList<SkillEffect> Effects { get; }

        public CardSkill(params SkillEffect[] effects)
        {
            Effects = effects;
        }
    }
}
