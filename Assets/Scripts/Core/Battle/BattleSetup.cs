using System.Collections.Generic;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>덱에 넣은 카드 한 장 (카드 번호 + 그 플레이어가 키운 레벨).</summary>
    public sealed class DeckCard
    {
        public int CardId { get; }
        public int Level { get; }

        public DeckCard(int cardId, int level = 1)
        {
            CardId = cardId;
            Level = level;
        }
    }

    public sealed class ArthurSetup
    {
        public string Name { get; set; }
        public Job Job { get; set; }
        public int MaxHp { get; set; }
        public int PhysicalDefense { get; set; }
        public int MagicDefense { get; set; }

        /// <summary>10장. 첫 장이 리더 카드다. 이름이 같은 카드는 1장만 넣을 수 있다.</summary>
        public IReadOnlyList<DeckCard> Deck { get; set; }
    }

    public sealed class PartSetup
    {
        public string Name { get; set; }
        public int MaxHp { get; set; }

        /// <summary>부위가 받는 속성별 피해 배율. 비어 있으면 본체 속성의 상성을 쓴다.</summary>
        public IReadOnlyDictionary<Element, double> ElementMultipliers { get; set; }
    }

    public sealed class EnemySetup
    {
        public string Name { get; set; }
        public Element Element { get; set; }
        public int MaxHp { get; set; }
        public int PhysicalDefense { get; set; }
        public int MagicDefense { get; set; }
        public IReadOnlyList<PartSetup> Parts { get; set; } = new PartSetup[0];
        public IEnemyBrain Brain { get; set; }
    }

    public sealed class BattleSetup
    {
        public IReadOnlyList<ArthurSetup> Arthurs { get; set; }
        public IReadOnlyList<EnemySetup> Enemies { get; set; }

        /// <summary>서버가 정하는 무작위 원천. 테스트에서는 결과가 정해진 값을 넣는다.</summary>
        public IBattleRandom Random { get; set; }

        /// <summary>false면 덱 순서 그대로 뽑는다 (테스트용).</summary>
        public bool ShuffleDecks { get; set; } = true;
    }
}
