using System;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Core.Battle;
using ChainKnights.Core.Cards;
using NUnit.Framework;

namespace ChainKnights.Core.Tests
{
    public class BattleSessionTests
    {
        // 카드 번호 (Docs/카드목록.md)
        private const int Marose = 1;        // 전사 화 ★1 코스트1: 자신 1턴 물리 피해 +190 (각성 +317, 드로우 +1)
        private const int Gaheris = 3;       // 전사 화 ★2 코스트3: 단일 물리 507
        private const int Pellinore = 7;     // 수호자 화 ★2 코스트2: 단일 물리 341
        private const int Bagdemagus = 11;   // 전사 화 ★2 코스트2: 단일 물리 341 (각성 558)
        private const int Lamorak = 16;      // 마법사 화 ★2 코스트2: 단일 마법 341
        private const int Dietrich = 28;     // 수호자 화 ★3 코스트2: 적 3턴 물리 피해 −175 (각성 −278)
        private const int Ebain = 131;       // 사제 광 ★3 코스트2: 자신 회복 508 (각성: 아군 1명 839)
        private const int Achilles = 171;    // 전사 암 ★3 코스트4: 단일 물리 746 (각성 1232 + 20% 행동 정지)
        private const int BlondIseult = 177; // 마법사 암 ★4 코스트2: 8턴 독 154 (각성 307)

        /// <summary>덱을 채우는 코스트 2짜리 카드들. 덱 앞쪽 5장이 첫 손패가 되도록 뒤에 붙인다.</summary>
        private static readonly int[] Fillers = { 221, 222, 223, 41, 87, 115, 155, 121, 118, 158 };

        private sealed class FixedRandom : IBattleRandom
        {
            private readonly double value;

            public FixedRandom(double value)
            {
                this.value = value;
            }

            public double NextDouble() => value;

            public int Next(int maxExclusive) => 0;
        }

        /// <summary>0.99면 크리티컬과 행동 정지가 나지 않고, 0이면 항상 난다.</summary>
        private static readonly IBattleRandom NoLuck = new FixedRandom(0.99);

        private static List<DeckCard> Deck(params int[] first)
        {
            var ids = first.Concat(Fillers.Where(f => !first.Contains(f))).Take(BattleRules.DeckSize);
            return ids.Select(id => new DeckCard(id)).ToList();
        }

        private static ArthurSetup Arthur(string name, Job job, List<DeckCard> deck, int maxHp = 5000)
        {
            return new ArthurSetup { Name = name, Job = job, MaxHp = maxHp, Deck = deck };
        }

        private static EnemySetup Dummy(
            Element element = Element.Light,
            int maxHp = 100000,
            int physicalDefense = 0,
            IEnemyBrain brain = null,
            params PartSetup[] parts)
        {
            return new EnemySetup
            {
                Name = "허수아비",
                Element = element,
                MaxHp = maxHp,
                PhysicalDefense = physicalDefense,
                Brain = brain,
                Parts = parts,
            };
        }

        private static IEnemyBrain EveryTurn(params EnemyAction[] actions) => new PatternBrain(_ => actions);

        private static BattleSession Battle(IBattleRandom random, EnemySetup enemy, params ArthurSetup[] arthurs)
        {
            return new BattleSession(new BattleSetup
            {
                Arthurs = arthurs,
                Enemies = new[] { enemy },
                Random = random,
                ShuffleDecks = false,
            });
        }

        private static int InHand(BattleSession battle, int arthurIndex, int cardId)
        {
            return battle.Arthurs[arthurIndex].Hand.First(c => c.Card.Id == cardId).InstanceId;
        }

        private static CardPlay Play(BattleSession battle, int arthurIndex, int cardId, TargetRef target)
        {
            return new CardPlay(InHand(battle, arthurIndex, cardId), target);
        }

        private static void SubmitOk(BattleSession battle, int arthurIndex, params CardPlay[] plays)
        {
            var result = battle.Submit(arthurIndex, plays);
            Assert.IsTrue(result.Success, result.Error);
        }

        private static int DamageTo(IEnumerable<BattleEvent> events, BattleSide side)
        {
            return events.Where(e => e.Type == BattleEventType.Damage && e.TargetSide == side).Sum(e => e.Value);
        }

        // ───────────── 코스트와 손패 ─────────────

        [Test]
        public void Cost_StartsAt3_GrowsByOne_CapsAt10()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck()));

            var costs = new List<int>();
            for (int turn = 1; turn <= 9; turn++)
            {
                battle.StartTurn();
                costs.Add(battle.Cost);
                battle.ResolveTurn();
            }

            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 10 }, costs);
        }

        [Test]
        public void Hand_StartsWithFive_AndDrawsOnePerTurn()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Pellinore, Marose)));
            battle.StartTurn();
            Assert.AreEqual(5, battle.Arthurs[0].Hand.Count);

            SubmitOk(battle, 0, Play(battle, 0, Pellinore, TargetRef.Enemy(0)), Play(battle, 0, Marose, TargetRef.None));
            battle.ResolveTurn();
            Assert.AreEqual(3, battle.Arthurs[0].Hand.Count);

            battle.StartTurn();
            Assert.AreEqual(4, battle.Arthurs[0].Hand.Count);
        }

        [Test]
        public void Cards_CycleThroughDiscard_WhenDrawPileRunsOut()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck()));

            for (int turn = 1; turn <= 8; turn++)
            {
                battle.StartTurn();
                var arthur = battle.Arthurs[0];
                Assert.AreEqual(BattleRules.DeckSize, arthur.Hand.Count + arthur.DrawPileCount + arthur.DiscardCount);
                Assert.That(arthur.Hand, Is.Not.Empty);

                SubmitOk(battle, 0, new CardPlay(arthur.Hand[0].InstanceId, TargetRef.Enemy(0)));
                battle.ResolveTurn();
            }
        }

        [Test]
        public void DrawEffect_AddsCardsNextTurn()
        {
            // 전사가 마로스를 쓰면 각성해서 다음 턴 드로우 +1.
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Warrior, Deck(Marose, Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Marose, TargetRef.None), Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));
            battle.ResolveTurn();

            battle.StartTurn();

            Assert.AreEqual(5, battle.Arthurs[0].Hand.Count); // 3장 + 기본 1장 + 드로우 1장
        }

        // ───────────── 제출 검증 ─────────────

        [Test]
        public void Submit_RejectsCardsOverTheTurnCost()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Pellinore, Bagdemagus)));
            battle.StartTurn();

            var result = battle.Submit(0, new[]
            {
                Play(battle, 0, Pellinore, TargetRef.Enemy(0)),
                Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)),
            });

            Assert.IsFalse(result.Success);
            StringAssert.Contains("코스트", result.Error);
        }

        [Test]
        public void Submit_RejectsCardsNotInHand()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck()));
            battle.StartTurn();
            int notInHand = battle.Arthurs[0].Hand.Max(c => c.InstanceId) + 1;

            Assert.IsFalse(battle.Submit(0, new[] { new CardPlay(notInHand, TargetRef.Enemy(0)) }).Success);
        }

        [Test]
        public void Submit_RequiresAnEnemyTarget_ForSingleTargetAttacks()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Pellinore)));
            battle.StartTurn();

            Assert.IsFalse(battle.Submit(0, new[] { Play(battle, 0, Pellinore, TargetRef.None) }).Success);
        }

        [Test]
        public void HasSubmitted_TellsOnlyWhetherAnArthurHasChosen()
        {
            var battle = Battle(NoLuck, Dummy(),
                Arthur("A", Job.Guardian, Deck(Pellinore)),
                Arthur("B", Job.Guardian, Deck()));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Pellinore, TargetRef.Enemy(0)));

            Assert.IsTrue(battle.HasSubmitted(0));
            Assert.IsFalse(battle.HasSubmitted(1));

            battle.ResolveTurn();
            battle.StartTurn();
            Assert.IsFalse(battle.HasSubmitted(0));
        }

        [Test]
        public void Submit_CanBeChangedBeforeResolving()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Pellinore, Marose)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Pellinore, TargetRef.Enemy(0)));
            SubmitOk(battle, 0, Play(battle, 0, Marose, TargetRef.None));

            var events = battle.ResolveTurn();

            Assert.AreEqual(0, DamageTo(events, BattleSide.Enemy));
        }

        // ───────────── 피해, 각성, 속성, 체인 ─────────────

        [Test]
        public void Damage_IsTheCardValue_WithoutAwakening()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));

            Assert.AreEqual(341, DamageTo(battle.ResolveTurn(), BattleSide.Enemy));
        }

        [Test]
        public void Damage_UsesAwakenedSkill_WhenJobMatches()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Warrior, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));

            var events = battle.ResolveTurn();

            Assert.AreEqual(558, DamageTo(events, BattleSide.Enemy));
            Assert.IsTrue(events.Single(e => e.Type == BattleEventType.CardPlayed).IsAwakened);
        }

        [TestCase(Element.Wind, 682)] // 화 → 풍: 200%
        [TestCase(Element.Ice, 171)]  // 빙 → 화: 화가 불리, 50%
        [TestCase(Element.Light, 341)]
        public void Damage_AppliesElementMultiplier(Element enemyElement, int expected)
        {
            var battle = Battle(NoLuck, Dummy(enemyElement), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));

            Assert.AreEqual(expected, DamageTo(battle.ResolveTurn(), BattleSide.Enemy));
        }

        [Test]
        public void Damage_Critical_IsOneAndAHalfTimes()
        {
            var battle = Battle(new FixedRandom(0.0), Dummy(), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));

            var damage = battle.ResolveTurn().Single(e => e.Type == BattleEventType.Damage);

            Assert.IsTrue(damage.IsCritical);
            Assert.AreEqual(512, damage.Value); // 341 × 1.5 = 511.5
        }

        [Test]
        public void Chain_TwoArthursSameElement_Multiplies1Point2()
        {
            var battle = Battle(NoLuck, Dummy(),
                Arthur("A", Job.Guardian, Deck(Bagdemagus)),
                Arthur("B", Job.Guardian, Deck(Lamorak)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));
            SubmitOk(battle, 1, Play(battle, 1, Lamorak, TargetRef.Enemy(0)));

            var events = battle.ResolveTurn();

            Assert.AreEqual(2, events.Single(e => e.Type == BattleEventType.Chain).Chain);
            Assert.AreEqual(409 * 2, DamageTo(events, BattleSide.Enemy)); // 341 × 1.2 = 409.2
        }

        [Test]
        public void Chain_IsNotFormed_ByOneArthursOwnCards()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Bagdemagus, Lamorak)));
            battle.StartTurn();
            battle.ResolveTurn();
            battle.StartTurn(); // 코스트 4
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)), Play(battle, 0, Lamorak, TargetRef.Enemy(0)));

            var events = battle.ResolveTurn();

            Assert.IsFalse(events.Any(e => e.Type == BattleEventType.Chain));
            Assert.AreEqual(341 * 2, DamageTo(events, BattleSide.Enemy));
        }

        [Test]
        public void SupportCards_ResolveBeforeAttacks_EvenIfChosenLater()
        {
            // 수호자가 쓰면 마로스는 각성하지 않아 자신 물리 피해 +190.
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, Deck(Bagdemagus, Marose)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)), Play(battle, 0, Marose, TargetRef.None));

            Assert.AreEqual(341 + 190, DamageTo(battle.ResolveTurn(), BattleSide.Enemy));
        }

        [TestCase(100, 241)]
        [TestCase(1000, 1)] // 방어가 피해보다 커도 최소 1
        public void Damage_IsReducedByDefense_ToAMinimumOfOne(int defense, int expected)
        {
            var battle = Battle(NoLuck, Dummy(physicalDefense: defense), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));

            Assert.AreEqual(expected, DamageTo(battle.ResolveTurn(), BattleSide.Enemy));
        }

        [Test]
        public void AllEnemiesAwakening_HitsEveryEnemy()
        {
            // 이터널 플레임(코스트 5)은 전사가 쓰면 적 전체 4885.
            var battle = new BattleSession(new BattleSetup
            {
                Arthurs = new[] { Arthur("A", Job.Warrior, Deck(37)) },
                Enemies = new[] { Dummy(), Dummy() },
                Random = NoLuck,
                ShuffleDecks = false,
            });
            battle.StartTurn();
            battle.ResolveTurn();
            battle.StartTurn();
            battle.ResolveTurn();
            battle.StartTurn(); // 코스트 5
            SubmitOk(battle, 0, Play(battle, 0, 37, TargetRef.Enemy(0)));

            var damages = battle.ResolveTurn().Where(e => e.Type == BattleEventType.Damage).ToList();

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, damages.Select(e => e.TargetIndex));
            Assert.That(damages.All(e => e.Value == 4885));
        }

        // ───────────── 적 행동, 부위, 디버프 ─────────────

        [Test]
        public void Enemy_AttacksTheTelegraphedArthur()
        {
            var brain = EveryTurn(EnemyAction.Attack("물기", StatType.Physical, 500));
            var battle = Battle(NoLuck, Dummy(brain: brain), Arthur("A", Job.Guardian, Deck()));
            battle.StartTurn();

            Assert.AreEqual(0, battle.Enemies[0].Intents.Single().TargetArthurIndex);
            battle.ResolveTurn();
            Assert.AreEqual(4500, battle.Arthurs[0].Hp);
        }

        [Test]
        public void DamageDown_ReducesEnemyAttack()
        {
            // 수호자가 쓰면 디트리히가 각성: 적 3턴 물리 피해 −278.
            var brain = EveryTurn(EnemyAction.Attack("물기", StatType.Physical, 500));
            var battle = Battle(NoLuck, Dummy(brain: brain), Arthur("A", Job.Guardian, Deck(Dietrich)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Dietrich, TargetRef.Enemy(0)));
            battle.ResolveTurn();

            Assert.AreEqual(5000 - (500 - 278), battle.Arthurs[0].Hp);
        }

        [Test]
        public void DestroyingAPart_HitsTheBody_AndCancelsThatPartsAction()
        {
            var arm = new PartSetup { Name = "팔", MaxHp = 300 };
            var brain = EveryTurn(EnemyAction.Attack("팔 휘두르기", StatType.Physical, 500, sourcePart: 0));
            var battle = Battle(NoLuck, Dummy(maxHp: 10000, brain: brain, parts: arm), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0, part: 0)));

            var events = battle.ResolveTurn();

            Assert.IsTrue(battle.Enemies[0].Parts[0].IsDestroyed);
            Assert.AreEqual(10000 - 341, battle.Enemies[0].Hp);
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.PartDestroyed));
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.EnemyActionCanceled));
            Assert.AreEqual(5000, battle.Arthurs[0].Hp);
        }

        [Test]
        public void Part_UsesItsOwnElementMultipliers()
        {
            // 아이스 드래곤의 팔: 화 25%.
            var arm = new PartSetup
            {
                Name = "팔",
                MaxHp = 10000,
                ElementMultipliers = new Dictionary<Element, double> { { Element.Fire, 0.25 } },
            };
            var battle = Battle(NoLuck, Dummy(Element.Ice, parts: arm), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0, part: 0)));

            Assert.AreEqual(85, DamageTo(battle.ResolveTurn(), BattleSide.Enemy)); // 341 × 0.25 = 85.25
        }

        [Test]
        public void Stun_CancelsTheEnemysActionsThatTurn()
        {
            var brain = EveryTurn(EnemyAction.Attack("물기", StatType.Physical, 500));
            var battle = Battle(new FixedRandom(0.0), Dummy(brain: brain), Arthur("A", Job.Warrior, Deck(Achilles)));
            battle.StartTurn();
            battle.ResolveTurn();
            Assert.AreEqual(4500, battle.Arthurs[0].Hp);

            battle.StartTurn(); // 코스트 4
            SubmitOk(battle, 0, Play(battle, 0, Achilles, TargetRef.Enemy(0)));
            var events = battle.ResolveTurn();
            Assert.IsTrue(events.Any(e => e.Type == BattleEventType.Stunned));
            Assert.AreEqual(4500, battle.Arthurs[0].Hp);

            battle.StartTurn();
            battle.ResolveTurn();
            Assert.AreEqual(4000, battle.Arthurs[0].Hp);
        }

        [Test]
        public void Poison_TicksAtTurnEnd_IgnoringDefense()
        {
            // 마법사가 쓰면 금발의 이졸데가 각성: 8턴 매 턴 독 307.
            var battle = Battle(NoLuck, Dummy(maxHp: 10000, physicalDefense: 1000), Arthur("A", Job.Mage, Deck(BlondIseult)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, BlondIseult, TargetRef.Enemy(0)));
            battle.ResolveTurn();
            Assert.AreEqual(10000 - 307, battle.Enemies[0].Hp);

            battle.StartTurn();
            battle.ResolveTurn();
            Assert.AreEqual(10000 - 307 * 2, battle.Enemies[0].Hp);
        }

        [Test]
        public void Heal_RestoresTheChosenAlly()
        {
            var brain = new PatternBrain(turn => turn == 1
                ? new[] { EnemyAction.Attack("브레스", StatType.Magic, 1000, targetsAll: true) }
                : Array.Empty<EnemyAction>());
            var battle = Battle(NoLuck, Dummy(brain: brain),
                Arthur("힐러", Job.Priest, Deck(Ebain), maxHp: 6000),
                Arthur("탱커", Job.Guardian, Deck(), maxHp: 6000));
            battle.StartTurn();
            battle.ResolveTurn();
            Assert.AreEqual(5000, battle.Arthurs[1].Hp);

            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Ebain, TargetRef.Ally(1)));
            battle.ResolveTurn();

            Assert.AreEqual(5000 + 839, battle.Arthurs[1].Hp);
            Assert.AreEqual(5000, battle.Arthurs[0].Hp);
        }

        // ───────────── 신호 ─────────────

        [Test]
        public void Signals_AreSharedDuringPlanning_AndClearedNextTurn()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Warrior, Deck()), Arthur("B", Job.Priest, Deck()));
            battle.StartTurn();

            Assert.IsTrue(battle.SendSignal(0, SignalKind.Attack).Success);
            Assert.IsTrue(battle.SendSignal(1, SignalKind.Chain, Element.Wind).Success);
            Assert.AreEqual(2, battle.Signals.Count);
            Assert.AreEqual(Element.Wind, battle.Signals[1].Element);

            battle.ResolveTurn();
            Assert.IsFalse(battle.SendSignal(0, SignalKind.Attack).Success);

            battle.StartTurn();
            Assert.That(battle.Signals, Is.Empty);
        }

        [Test]
        public void Signals_AreLimitedPerTurn()
        {
            var battle = Battle(NoLuck, Dummy(), Arthur("A", Job.Warrior, Deck()));
            battle.StartTurn();

            for (int i = 0; i < BattleRules.SignalsPerTurn; i++)
                Assert.IsTrue(battle.SendSignal(0, SignalKind.NeedHeal).Success);
            Assert.IsFalse(battle.SendSignal(0, SignalKind.NeedHeal).Success);
        }

        [Test]
        public void Signals_NeedAnElementForChain_AndALivePartForFocus()
        {
            var claw = new PartSetup { Name = "얼음 발톱", MaxHp = 100 };
            var battle = Battle(NoLuck, Dummy(parts: claw), Arthur("A", Job.Warrior, Deck()));
            battle.StartTurn();

            Assert.IsFalse(battle.SendSignal(0, SignalKind.Chain).Success);
            Assert.IsFalse(battle.SendSignal(0, SignalKind.FocusPart, partIndex: 3).Success);
            Assert.IsTrue(battle.SendSignal(0, SignalKind.FocusPart, partIndex: 0).Success);
        }

        // ───────────── 승패와 덱 규칙 ─────────────

        [Test]
        public void Victory_WhenAllEnemiesFall()
        {
            var battle = Battle(NoLuck, Dummy(maxHp: 300), Arthur("A", Job.Guardian, Deck(Bagdemagus)));
            battle.StartTurn();
            SubmitOk(battle, 0, Play(battle, 0, Bagdemagus, TargetRef.Enemy(0)));
            battle.ResolveTurn();

            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
            Assert.AreEqual(BattlePhase.Ended, battle.Phase);
            Assert.Throws<InvalidOperationException>(() => battle.StartTurn());
        }

        [Test]
        public void Defeat_WhenAllArthursFall()
        {
            var brain = EveryTurn(EnemyAction.Attack("물기", StatType.Physical, 500));
            var battle = Battle(NoLuck, Dummy(brain: brain), Arthur("A", Job.Guardian, Deck(), maxHp: 400));
            battle.StartTurn();
            battle.ResolveTurn();

            Assert.AreEqual(BattleOutcome.Defeat, battle.Outcome);
        }

        [Test]
        public void Deck_MustHaveTenCards_WithUniqueNames()
        {
            var nine = Deck().Take(9).ToList();
            var duplicate = Deck(Pellinore).Take(9).Append(new DeckCard(Pellinore)).ToList();

            Assert.Throws<ArgumentException>(() => Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, nine)));
            Assert.Throws<ArgumentException>(() => Battle(NoLuck, Dummy(), Arthur("A", Job.Guardian, duplicate)));
        }
    }
}
