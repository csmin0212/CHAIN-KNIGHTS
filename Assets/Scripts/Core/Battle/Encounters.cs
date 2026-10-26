using System.Collections.Generic;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    public enum RaidDifficulty
    {
        Medium,
        Hard,
        Extreme,
    }

    /// <summary>적 구성. 수치는 기획서 7장 "몬스터"를 따른다.</summary>
    public static class Encounters
    {
        private static readonly Dictionary<Element, string> ElementNames = new Dictionary<Element, string>
        {
            { Element.Fire, "화" },
            { Element.Ice, "빙" },
            { Element.Wind, "풍" },
            { Element.Light, "광" },
            { Element.Dark, "암" },
        };

        /// <summary>노멀 퀘스트 잡몹. HP 2,000~4,000, 단일 공격 300~500.</summary>
        public static EnemySetup Golem(Element element, int maxHp = 3000, int power = 400)
        {
            var bite = EnemyAction.Attack("구골 내려찍기", StatType.Physical, power);
            return new EnemySetup
            {
                Name = $"{ElementNames[element]}형 구골",
                Element = element,
                MaxHp = maxHp,
                Brain = new PatternBrain(_ => new[] { bite }),
            };
        }

        /// <summary>시작의 대지 첫 웨이브: 화·풍 구골 3마리.</summary>
        public static IReadOnlyList<EnemySetup> GolemPack()
        {
            return new[] { Golem(Element.Fire), Golem(Element.Wind), Golem(Element.Fire) };
        }

        /// <summary>
        /// 레이드 보스 빙결룡 글라키에스. 중급은 본체와 얼음 발톱, 상급부터 서리 날개가 붙는다.
        /// 발톱과 날개는 풍 150%, 화 25%, 빙·광·암 50%를 받는다.
        /// </summary>
        public static EnemySetup Glacies(RaidDifficulty difficulty)
        {
            int bodyHp, clawHp, wingHp, frostBolt, breath, claw;
            switch (difficulty)
            {
                case RaidDifficulty.Hard:
                    bodyHp = 100000; clawHp = 30000; wingHp = 30000; frostBolt = 2000; breath = 3000; claw = 1750;
                    break;
                case RaidDifficulty.Extreme:
                    bodyHp = 250000; clawHp = 80000; wingHp = 80000; frostBolt = 4000; breath = 6000; claw = 3500;
                    break;
                default:
                    bodyHp = 40000; clawHp = 12000; wingHp = 0; frostBolt = 800; breath = 1200; claw = 700;
                    break;
            }

            var partMultipliers = new Dictionary<Element, double>
            {
                { Element.Wind, 1.5 },
                { Element.Fire, 0.25 },
                { Element.Ice, 0.5 },
                { Element.Light, 0.5 },
                { Element.Dark, 0.5 },
            };
            var parts = new List<PartSetup> { new PartSetup { Name = "얼음 발톱", MaxHp = clawHp, ElementMultipliers = partMultipliers } };
            bool hasWing = wingHp > 0;
            if (hasWing)
                parts.Add(new PartSetup { Name = "서리 날개", MaxHp = wingHp, ElementMultipliers = partMultipliers });

            var frostBoltAction = EnemyAction.Attack("냉기탄", StatType.Magic, frostBolt);
            var breathAction = EnemyAction.Attack("빙결 브레스", StatType.Magic, breath, targetsAll: true);
            var clawAction = EnemyAction.Attack("발톱 찍기", StatType.Physical, claw, sourcePart: 0);
            // 날개의 강화는 다음 턴까지 남아서 발톱 찍기가 2배가 된다.
            var wingAction = EnemyAction.PowerUp("서리 날갯짓", StatType.Physical, claw, turns: 2, sourcePart: 1);

            var brain = new PatternBrain(turn =>
            {
                var actions = new List<EnemyAction> { frostBoltAction };
                if (turn % 3 == 0)
                    actions.Add(breathAction);
                if (hasWing && turn % 2 == 0)
                    actions.Add(wingAction);
                actions.Add(clawAction);
                return actions;
            });

            return new EnemySetup
            {
                Name = "빙결룡 글라키에스",
                Element = Element.Ice,
                MaxHp = bodyHp,
                Parts = parts,
                Brain = brain,
            };
        }
    }

    /// <summary>처음 시작할 때 받는 카드로 직업별 덱을 짠다.</summary>
    public static class StarterDecks
    {
        /// <summary>
        /// 내 직업의 스타터 카드를 먼저 넣고(각성), 남는 칸은 공용 카드와 다른 직업의 스타터 카드로 채운다.
        /// maxLevel이 true면 카드를 최대 레벨로 넣는다 (테스트 플레이용).
        /// </summary>
        public static List<DeckCard> For(Job job, bool maxLevel = false)
        {
            var picked = new List<CardDefinition>();
            void AddFrom(IEnumerable<CardDefinition> cards)
            {
                foreach (var card in cards)
                {
                    if (picked.Count >= BattleRules.DeckSize)
                        return;
                    if (!picked.Exists(c => c.NameKo == card.NameKo))
                        picked.Add(card);
                }
            }

            var starters = new List<CardDefinition>(CardCatalog.StarterCards);
            // 높은 성, 높은 코스트부터 넣어서 덱의 화력을 챙긴다.
            starters.Sort((a, b) => b.Rarity != a.Rarity ? b.Rarity.CompareTo(a.Rarity) : b.Cost.CompareTo(a.Cost));
            AddFrom(starters.FindAll(c => c.Job == job));
            AddFrom(starters.FindAll(c => c.Job == Job.None));
            AddFrom(starters);

            return picked.ConvertAll(c => new DeckCard(c.Id, maxLevel ? c.MaxLevel : 1));
        }
    }
}
