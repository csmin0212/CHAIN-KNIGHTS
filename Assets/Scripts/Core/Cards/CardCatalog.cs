using System.Collections.Generic;
using System.Linq;

namespace ChainKnights.Core.Cards
{
    /// <summary>
    /// 게임에 들어 있는 모든 카드. 카드 목록은 CardCatalog.Generated.cs에 있고,
    /// Tools/CardConverter/convert_cards.py가 Docs/원작_초기카드.csv에서 만든다. 직접 고치지 말 것.
    /// </summary>
    public static partial class CardCatalog
    {
        private static readonly CardDefinition[] cards = CreateCards();
        private static readonly Dictionary<int, CardDefinition> byId = cards.ToDictionary(c => c.Id);

        public static IReadOnlyList<CardDefinition> All => cards;

        public static CardDefinition Get(int id)
        {
            if (!byId.TryGetValue(id, out var card))
                throw new KeyNotFoundException($"카드 {id}번이 없습니다.");
            return card;
        }

        public static bool TryGet(int id, out CardDefinition card)
        {
            return byId.TryGetValue(id, out card);
        }

        /// <summary>처음 시작할 때 받는 카드 (경비용 구골 3장 + 모든 ★1, ★2 카드).</summary>
        public static IEnumerable<CardDefinition> StarterCards => cards.Where(c => c.Source == CardSource.Starter);
    }
}
