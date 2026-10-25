namespace ChainKnights.Core.Cards
{
    /// <summary>아서 타입(직업). 카드의 직업 타입이 아서와 같으면 각성 스킬이 발동한다.</summary>
    public enum Job
    {
        /// <summary>직업 없음 (경비용 구골 같은 공용 카드).</summary>
        None,
        /// <summary>용병: 물리 딜러.</summary>
        Mercenary,
        /// <summary>부호: 서포터 + 물리.</summary>
        Tycoon,
        /// <summary>도적: 마법 딜러 + 디버퍼.</summary>
        Thief,
        /// <summary>가희: 힐러 + 버퍼.</summary>
        Diva,
    }

    /// <summary>속성. 화 → 풍 → 빙 → 화 순환, 광 ⇄ 암은 서로 강하다.</summary>
    public enum Element
    {
        Fire,
        Ice,
        Wind,
        Light,
        Dark,
    }

    /// <summary>스킬 분류. 서버는 이 순서와 별개로 지원 → 방어 → 치유 → 방해 → 공격 순으로 처리한다.</summary>
    public enum SkillCategory
    {
        Physical,
        Magic,
        Support,
        Defense,
        Heal,
        Hinder,
    }

    public enum EffectKind
    {
        /// <summary>피해. Stat = 물리/마법.</summary>
        Damage,
        /// <summary>즉시 회복.</summary>
        Heal,
        /// <summary>Turns 동안 매 턴 회복.</summary>
        HealOverTime,
        /// <summary>Turns 동안 주는 피해 증가 (고정값). Stat = 물리/마법/전체.</summary>
        DamageUp,
        /// <summary>Turns 동안 방어 증가 (고정값). Stat = 물리/마법.</summary>
        DefenseUp,
        /// <summary>Turns 동안 대상이 주는 피해 감소 (고정값).</summary>
        DamageDown,
        /// <summary>Turns 동안 대상의 방어 감소 (고정값).</summary>
        DefenseDown,
        /// <summary>Turns 동안 매 턴 피해.</summary>
        Poison,
        /// <summary>카드를 Value장 더 뽑는다.</summary>
        Draw,
        /// <summary>이 카드의 공격이 Chance 확률로 크리티컬.</summary>
        CritRateUp,
        /// <summary>Chance 확률로 대상의 행동을 Turns 동안 멈춘다.</summary>
        Stun,
        /// <summary>이 카드로 준 피해의 Chance 비율만큼 회복.</summary>
        Lifesteal,
        /// <summary>Turns 동안 회복량이 Chance 비율만큼 증가.</summary>
        HealBoost,
    }

    public enum EffectTarget
    {
        Self,
        AllySingle,
        AllAllies,
        EnemySingle,
        AllEnemies,
    }

    public enum StatType
    {
        None,
        Physical,
        Magic,
        All,
    }

    public enum CardSource
    {
        /// <summary>처음 시작할 때 받는 카드 (★1, ★2).</summary>
        Starter,
        /// <summary>프리미엄 뽑기.</summary>
        Gacha,
        /// <summary>레이드 보스 드랍.</summary>
        RaidDrop,
    }
}
