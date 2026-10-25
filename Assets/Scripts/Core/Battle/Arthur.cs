using System.Collections.Generic;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>전투 중인 카드 한 장. 같은 카드라도 아서마다 레벨이 다르다.</summary>
    public sealed class CardInstance
    {
        /// <summary>이 전투 안에서 유일한 번호. 카드를 낼 때 이 번호로 고른다.</summary>
        public int InstanceId { get; }
        public CardDefinition Card { get; }
        public int Level { get; }

        public CardInstance(int instanceId, CardDefinition card, int level)
        {
            InstanceId = instanceId;
            Card = card;
            Level = level;
        }

        public int ValueOf(SkillEffect effect) => effect.ValueAt(Level, Card.MaxLevel);
    }

    /// <summary>플레이어 캐릭터. 덱에서 손패를 뽑아 카드를 낸다.</summary>
    public sealed class Arthur : BattleUnit
    {
        private readonly List<CardInstance> drawPile;
        private readonly List<CardInstance> hand = new List<CardInstance>();
        private readonly List<CardInstance> discard = new List<CardInstance>();
        private readonly List<CardInstance> played = new List<CardInstance>();

        public override BattleSide Side => BattleSide.Ally;
        public Job Job { get; }
        public IReadOnlyList<CardInstance> Hand => hand;
        public int DrawPileCount => drawPile.Count;
        public int DiscardCount => discard.Count;

        /// <summary>드로우 효과로 다음 턴 시작에 더 뽑을 장수.</summary>
        public int PendingDraws { get; internal set; }

        internal Arthur(int index, string name, Job job, int maxHp, int physicalDefense, int magicDefense, List<CardInstance> deck)
            : base(index, name, maxHp, physicalDefense, magicDefense)
        {
            Job = job;
            drawPile = deck;
        }

        public CardInstance FindInHand(int instanceId) => hand.Find(c => c.InstanceId == instanceId);

        internal void Draw(int count, IBattleRandom random)
        {
            for (int i = 0; i < count && hand.Count < BattleRules.HandSize; i++)
            {
                if (drawPile.Count == 0)
                {
                    if (discard.Count == 0)
                        return;
                    drawPile.AddRange(discard);
                    discard.Clear();
                    Shuffle(drawPile, random);
                }
                hand.Add(drawPile[0]);
                drawPile.RemoveAt(0);
            }
        }

        /// <summary>낸 카드는 턴이 끝날 때까지 손패에서 빠져 있다가 버림 더미로 간다.</summary>
        internal void Play(CardInstance card)
        {
            hand.Remove(card);
            played.Add(card);
        }

        internal void DiscardPlayed()
        {
            discard.AddRange(played);
            played.Clear();
        }

        internal static void Shuffle(List<CardInstance> cards, IBattleRandom random)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }
        }
    }
}
