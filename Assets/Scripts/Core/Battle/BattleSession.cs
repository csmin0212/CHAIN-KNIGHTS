using System;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    public enum BattlePhase
    {
        /// <summary>아직 첫 턴을 시작하지 않았다.</summary>
        NotStarted,
        /// <summary>플레이어들이 카드를 고르는 중이다.</summary>
        Planning,
        /// <summary>턴 처리가 끝났다. 다음 턴을 시작할 수 있다.</summary>
        Resolved,
        /// <summary>승리 또는 패배로 전투가 끝났다.</summary>
        Ended,
    }

    /// <summary>
    /// 한 판의 협력 턴제 전투. 네트워크와 Unity에 의존하지 않아서 서버가 그대로 들고 판정한다.
    /// 진행 순서: StartTurn → 각 아서가 Submit → ResolveTurn → 다시 StartTurn.
    /// </summary>
    public sealed class BattleSession
    {
        private readonly IBattleRandom random;
        private readonly List<Arthur> arthurs = new List<Arthur>();
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly Dictionary<int, IReadOnlyList<CardPlay>> submissions = new Dictionary<int, IReadOnlyList<CardPlay>>();

        public int Turn { get; private set; }
        public int Cost => BattleRules.CostForTurn(Turn);
        public BattlePhase Phase { get; private set; } = BattlePhase.NotStarted;
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public IReadOnlyList<Arthur> Arthurs => arthurs;
        public IReadOnlyList<Enemy> Enemies => enemies;

        /// <summary>
        /// 이번 턴 각 아서가 고른 카드 (아서 번호 → 카드). 서버만 본다.
        /// 아군이 무엇을 냈는지 모르는 것이 이 게임의 핵심 재미라서, 클라이언트에는 HasSubmitted만 알려 준다.
        /// </summary>
        public IReadOnlyDictionary<int, IReadOnlyList<CardPlay>> Submissions => submissions;

        /// <summary>이 아서가 이번 턴 선택을 마쳤는지. 다른 플레이어 화면에는 이것만 보여준다.</summary>
        public bool HasSubmitted(int arthurIndex) => submissions.ContainsKey(arthurIndex);

        public BattleSession(BattleSetup setup)
        {
            if (setup == null)
                throw new ArgumentNullException(nameof(setup));
            if (setup.Arthurs == null || setup.Arthurs.Count < 1 || setup.Arthurs.Count > BattleRules.MaxArthurs)
                throw new ArgumentException($"아서는 1~{BattleRules.MaxArthurs}명이어야 합니다.", nameof(setup));
            if (setup.Enemies == null || setup.Enemies.Count < 1)
                throw new ArgumentException("적이 최소 1명 있어야 합니다.", nameof(setup));

            random = setup.Random ?? new SeededBattleRandom(Environment.TickCount);

            int nextInstanceId = 1;
            for (int i = 0; i < setup.Arthurs.Count; i++)
            {
                var a = setup.Arthurs[i];
                var deck = BuildDeck(a, ref nextInstanceId);
                if (setup.ShuffleDecks)
                    Arthur.Shuffle(deck, random);
                arthurs.Add(new Arthur(i, a.Name, a.Job, a.MaxHp, a.PhysicalDefense, a.MagicDefense, deck));
            }
            for (int i = 0; i < setup.Enemies.Count; i++)
                enemies.Add(new Enemy(i, setup.Enemies[i]));
        }

        private static List<CardInstance> BuildDeck(ArthurSetup arthur, ref int nextInstanceId)
        {
            if (arthur.Deck == null || arthur.Deck.Count != BattleRules.DeckSize)
                throw new ArgumentException($"{arthur.Name}: 덱은 {BattleRules.DeckSize}장이어야 합니다.");

            var deck = new List<CardInstance>();
            foreach (var entry in arthur.Deck)
            {
                var card = CardCatalog.Get(entry.CardId);
                if (entry.Level < 1 || entry.Level > card.MaxLevel)
                    throw new ArgumentException($"{arthur.Name}: {card.NameKo}의 레벨은 1~{card.MaxLevel}이어야 합니다.");
                deck.Add(new CardInstance(nextInstanceId++, card, entry.Level));
            }
            if (deck.Select(c => c.Card.NameKo).Distinct().Count() != deck.Count)
                throw new ArgumentException($"{arthur.Name}: 이름이 같은 카드는 덱에 1장만 넣을 수 있습니다.");
            return deck;
        }

        // ───────────────────────── 턴 시작 ─────────────────────────

        /// <summary>코스트가 오르고, 카드를 뽑고, 적이 이번 턴 행동을 예고한다.</summary>
        public IReadOnlyList<BattleEvent> StartTurn()
        {
            if (Outcome != BattleOutcome.Ongoing)
                throw new InvalidOperationException("전투가 이미 끝났습니다.");
            if (Phase == BattlePhase.Planning)
                throw new InvalidOperationException("이번 턴을 먼저 처리해야 합니다 (ResolveTurn).");

            Turn++;
            submissions.Clear();
            var events = new List<BattleEvent>
            {
                new BattleEvent { Type = BattleEventType.TurnStarted, Value = Cost, Message = $"{Turn}턴 시작 (코스트 {Cost})" },
            };

            foreach (var arthur in arthurs.Where(a => a.IsAlive))
            {
                // 첫 턴은 손패 5장으로 시작하고, 그다음부터 매 턴 1장 + 드로우 효과만큼 뽑는다.
                arthur.Draw(Turn == 1 ? BattleRules.HandSize : 1 + arthur.PendingDraws, random);
                arthur.PendingDraws = 0;
            }

            foreach (var enemy in enemies.Where(e => e.IsAlive))
            {
                if (enemy.StunTurns > 0)
                {
                    enemy.SetIntents(Enumerable.Empty<EnemyIntent>());
                    continue;
                }
                var actions = enemy.Brain?.Decide(enemy, Turn, random) ?? Array.Empty<EnemyAction>();
                enemy.SetIntents(actions.Select(a => new EnemyIntent(a, NeedsArthurTarget(a) ? RandomAliveArthur().Index : -1)).ToList());
            }

            Phase = BattlePhase.Planning;
            return events;
        }

        private static bool NeedsArthurTarget(EnemyAction action)
        {
            return action.Kind == EnemyActionKind.Attack && !action.TargetsAll;
        }

        // ───────────────────────── 카드 제출 ─────────────────────────

        /// <summary>
        /// 아서가 이번 턴에 낼 카드를 고른다. 제한 시간 안에는 몇 번이든 다시 고를 수 있고, 마지막 선택이 쓰인다.
        /// 제출하지 않은 아서는 이번 턴을 넘긴다.
        /// </summary>
        public SubmitResult Submit(int arthurIndex, IReadOnlyList<CardPlay> plays)
        {
            if (Phase != BattlePhase.Planning)
                return SubmitResult.Fail("지금은 카드를 낼 수 없습니다.");
            if (arthurIndex < 0 || arthurIndex >= arthurs.Count)
                return SubmitResult.Fail("없는 아서입니다.");

            var arthur = arthurs[arthurIndex];
            if (!arthur.IsAlive)
                return SubmitResult.Fail("쓰러진 아서는 카드를 낼 수 없습니다.");

            plays = plays ?? Array.Empty<CardPlay>();
            if (plays.Select(p => p.InstanceId).Distinct().Count() != plays.Count)
                return SubmitResult.Fail("같은 카드를 두 번 낼 수 없습니다.");

            int totalCost = 0;
            foreach (var play in plays)
            {
                var card = arthur.FindInHand(play.InstanceId);
                if (card == null)
                    return SubmitResult.Fail("손패에 없는 카드입니다.");
                totalCost += card.Card.Cost;

                string targetError = ValidateTarget(card.Card.SkillFor(arthur.Job), play.Target);
                if (targetError != null)
                    return SubmitResult.Fail($"{card.Card.NameKo}: {targetError}");
            }
            if (totalCost > Cost)
                return SubmitResult.Fail($"코스트가 부족합니다 ({totalCost}/{Cost}).");

            submissions[arthurIndex] = plays.ToList();
            return SubmitResult.Ok;
        }

        private string ValidateTarget(CardSkill skill, TargetRef target)
        {
            if (skill.Effects.Any(e => e.Target == EffectTarget.EnemySingle))
            {
                var enemy = target.Side == BattleSide.Enemy ? EnemyAt(target.Index) : null;
                if (enemy == null || !enemy.IsAlive)
                    return "살아 있는 적을 대상으로 골라야 합니다.";
                if (target.Part >= 0 && enemy.Part(target.Part) == null)
                    return "없는 부위입니다.";
            }
            if (skill.Effects.Any(e => e.Target == EffectTarget.AllySingle))
            {
                var ally = target.Side == BattleSide.Ally ? ArthurAt(target.Index) : null;
                if (ally == null || !ally.IsAlive)
                    return "살아 있는 아군을 대상으로 골라야 합니다.";
            }
            return null;
        }

        // ───────────────────────── 턴 처리 ─────────────────────────

        /// <summary>모든 아서의 카드를 처리하고, 적이 행동하고, 지속 효과가 돈다.</summary>
        public IReadOnlyList<BattleEvent> ResolveTurn()
        {
            if (Phase != BattlePhase.Planning)
                throw new InvalidOperationException("StartTurn을 먼저 호출해야 합니다.");

            var events = new List<BattleEvent>();
            var plays = CollectPlays();
            var chains = CountChains(plays, events);

            ResolvePlayerPhase(plays, chains, events);
            if (!UpdateOutcome(events))
            {
                ResolveEnemyPhase(events);
                if (!UpdateOutcome(events))
                {
                    EndTurn(events);
                    UpdateOutcome(events);
                }
            }

            foreach (var arthur in arthurs)
                arthur.DiscardPlayed();
            Phase = Outcome == BattleOutcome.Ongoing ? BattlePhase.Resolved : BattlePhase.Ended;
            return events;
        }

        private sealed class PendingPlay
        {
            public Arthur Arthur;
            public CardInstance Card;
            public TargetRef Target;
            public int Priority;
            public double ArthurKey;
            public int Order;
        }

        /// <summary>
        /// 처리 순서: 분류(지원 → 방어 → 치유 → 방해 → 공격) 순.
        /// 같은 분류 안에서 내 카드끼리는 고른 순서대로, 다른 아서와는 무작위 순서로 처리한다.
        /// </summary>
        private List<PendingPlay> CollectPlays()
        {
            var plays = new List<PendingPlay>();
            foreach (var entry in submissions.OrderBy(s => s.Key))
            {
                var arthur = arthurs[entry.Key];
                double arthurKey = random.NextDouble();
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    var play = entry.Value[i];
                    var card = arthur.FindInHand(play.InstanceId);
                    plays.Add(new PendingPlay
                    {
                        Arthur = arthur,
                        Card = card,
                        Target = play.Target,
                        Priority = BattleRules.ResolvePriority(card.Card.Category),
                        ArthurKey = arthurKey,
                        Order = i,
                    });
                }
            }

            var ordered = plays.OrderBy(p => p.Priority).ThenBy(p => p.ArthurKey).ThenBy(p => p.Order).ToList();
            foreach (var play in ordered)
                play.Arthur.Play(play.Card);
            return ordered;
        }

        /// <summary>속성마다 그 속성 카드를 낸 서로 다른 아서의 수. 한 아서가 여러 장 내도 1로 센다.</summary>
        private static Dictionary<Element, int> CountChains(List<PendingPlay> plays, List<BattleEvent> events)
        {
            var chains = new Dictionary<Element, int>();
            foreach (var group in plays.GroupBy(p => p.Card.Card.Element))
            {
                int chain = group.Select(p => p.Arthur.Index).Distinct().Count();
                chains[group.Key] = chain;
                if (chain >= 2)
                {
                    events.Add(new BattleEvent
                    {
                        Type = BattleEventType.Chain,
                        Element = group.Key,
                        Chain = chain,
                        Message = $"{group.Key} {chain}체인 (×{BattleRules.ChainMultiplier(chain):0.0})",
                    });
                }
            }
            return chains;
        }

        private sealed class PlayContext
        {
            public Arthur Arthur;
            public CardInstance Card;
            public TargetRef Target;
            public int Chain;
            public double CritChance;
        }

        private void ResolvePlayerPhase(List<PendingPlay> plays, Dictionary<Element, int> chains, List<BattleEvent> events)
        {
            foreach (var play in plays)
            {
                if (!play.Arthur.IsAlive)
                    continue;
                if (enemies.All(e => !e.IsAlive))
                    return;

                var card = play.Card.Card;
                bool awakened = card.IsAwakenedFor(play.Arthur.Job);
                var skill = card.SkillFor(play.Arthur.Job);
                var context = new PlayContext
                {
                    Arthur = play.Arthur,
                    Card = play.Card,
                    Target = play.Target,
                    Chain = chains[card.Element],
                    CritChance = skill.Effects
                        .Where(e => e.Kind == EffectKind.CritRateUp)
                        .Select(e => (double)e.Chance)
                        .DefaultIfEmpty(BattleRules.BaseCritChance)
                        .Max(),
                };

                events.Add(new BattleEvent
                {
                    Type = BattleEventType.CardPlayed,
                    ActorSide = BattleSide.Ally,
                    ActorIndex = play.Arthur.Index,
                    CardId = card.Id,
                    Element = card.Element,
                    Chain = context.Chain,
                    IsAwakened = awakened,
                    Message = $"{play.Arthur.Name}: {card.NameKo}{(awakened ? " (각성)" : "")}",
                });

                int damageDealt = 0;
                foreach (var effect in skill.Effects)
                    damageDealt += ApplyEffect(context, effect, damageDealt, events);
            }
        }

        /// <summary>효과 하나를 적용하고, 이 효과로 적에게 준 피해를 돌려준다 (흡혈 계산용).</summary>
        private int ApplyEffect(PlayContext context, SkillEffect effect, int damageSoFar, List<BattleEvent> events)
        {
            int value = context.Card.ValueOf(effect);
            double chainMultiplier = BattleRules.ChainMultiplier(context.Chain);
            int chainFlat = BattleRules.ChainFlatBonus(context.Chain);
            var actor = context.Arthur;

            switch (effect.Kind)
            {
                case EffectKind.Damage:
                {
                    int total = 0;
                    foreach (var (enemy, part) in DamageTargets(effect.Target, context.Target))
                        total += DealCardDamage(context, effect.Stat, value, enemy, part, events);
                    return total;
                }

                case EffectKind.Heal:
                {
                    int amount = Scale(value, chainMultiplier * (1 + actor.HealBoostRatio));
                    foreach (var ally in AllyTargets(effect.Target, context))
                        AddHealEvent(events, BattleEventType.Heal, actor, ally, ally.Heal(amount));
                    return 0;
                }

                case EffectKind.HealOverTime:
                {
                    int amount = Scale(value, chainMultiplier * (1 + actor.HealBoostRatio));
                    foreach (var ally in AllyTargets(effect.Target, context))
                        ApplyStatus(events, actor, ally, new StatusEffect(effect.Kind, StatType.None, amount, effect.Turns));
                    return 0;
                }

                case EffectKind.DamageUp:
                case EffectKind.DefenseUp:
                    foreach (var ally in AllyTargets(effect.Target, context))
                        ApplyStatus(events, actor, ally, new StatusEffect(effect.Kind, effect.Stat, value + chainFlat, effect.Turns));
                    return 0;

                case EffectKind.DamageDown:
                case EffectKind.DefenseDown:
                    foreach (var enemy in EnemyTargets(effect.Target, context.Target))
                        ApplyStatus(events, actor, enemy, new StatusEffect(effect.Kind, effect.Stat, value + chainFlat, effect.Turns));
                    return 0;

                case EffectKind.Poison:
                    foreach (var enemy in EnemyTargets(effect.Target, context.Target))
                        ApplyStatus(events, actor, enemy, new StatusEffect(effect.Kind, StatType.None, Scale(value, chainMultiplier), effect.Turns));
                    return 0;

                case EffectKind.Draw:
                    foreach (var ally in AllyTargets(effect.Target, context))
                    {
                        ally.PendingDraws += effect.BaseValue;
                        events.Add(new BattleEvent
                        {
                            Type = BattleEventType.DrawQueued,
                            ActorSide = BattleSide.Ally,
                            ActorIndex = actor.Index,
                            TargetSide = BattleSide.Ally,
                            TargetIndex = ally.Index,
                            Value = effect.BaseValue,
                            Message = $"{ally.Name}: 다음 턴 드로우 +{effect.BaseValue}",
                        });
                    }
                    return 0;

                case EffectKind.Stun:
                    foreach (var enemy in EnemyTargets(effect.Target, context.Target))
                    {
                        if (random.NextDouble() >= effect.Chance)
                            continue;
                        enemy.StunTurns = Math.Max(enemy.StunTurns, effect.Turns);
                        events.Add(new BattleEvent
                        {
                            Type = BattleEventType.Stunned,
                            ActorSide = BattleSide.Ally,
                            ActorIndex = actor.Index,
                            TargetSide = BattleSide.Enemy,
                            TargetIndex = enemy.Index,
                            Value = effect.Turns,
                            Message = $"{enemy.Name}: {effect.Turns}턴 행동 정지",
                        });
                    }
                    return 0;

                case EffectKind.Lifesteal:
                    AddHealEvent(events, BattleEventType.Heal, actor, actor, actor.Heal(Scale(damageSoFar, effect.Chance)));
                    return 0;

                case EffectKind.HealBoost:
                    ApplyStatus(events, actor, actor, new StatusEffect(effect.Kind, StatType.None, 0, effect.Turns, effect.Chance));
                    return 0;

                case EffectKind.CritRateUp:
                    // 이 카드의 크리티컬 확률로 이미 반영했다.
                    return 0;

                default:
                    throw new NotSupportedException($"처리하지 않는 효과: {effect.Kind}");
            }
        }

        /// <summary>
        /// 카드 피해 = (카드 수치 + 데미지 업) × 체인 × 속성 × 크리티컬 − 방어. 최소 1.
        /// 부위를 노리면 부위와 본체가 같은 피해를 받는다.
        /// </summary>
        private int DealCardDamage(PlayContext context, StatType stat, int value, Enemy enemy, EnemyPart part, List<BattleEvent> events)
        {
            var card = context.Card.Card;
            double raw = value + context.Arthur.StatusTotal(EffectKind.DamageUp, stat);
            double element = part != null
                ? part.ElementMultiplier(card.Element, enemy.Element)
                : ElementChart.Multiplier(card.Element, enemy.Element);
            bool critical = random.NextDouble() < context.CritChance;
            double total = raw * BattleRules.ChainMultiplier(context.Chain) * element * (critical ? BattleRules.CritMultiplier : 1.0);
            int damage = Math.Max(Scale(total, 1.0) - enemy.Defense(stat), 1);

            int dealt = enemy.TakeDamage(damage);
            events.Add(new BattleEvent
            {
                Type = BattleEventType.Damage,
                ActorSide = BattleSide.Ally,
                ActorIndex = context.Arthur.Index,
                TargetSide = BattleSide.Enemy,
                TargetIndex = enemy.Index,
                PartIndex = part?.Index ?? -1,
                CardId = card.Id,
                Element = card.Element,
                Value = damage,
                Chain = context.Chain,
                IsCritical = critical,
                Message = $"{enemy.Name}{(part != null ? " " + part.Name : "")}에게 {damage} 피해{(critical ? " (크리티컬)" : "")}",
            });

            if (part != null && part.TakeDamage(damage))
            {
                events.Add(new BattleEvent
                {
                    Type = BattleEventType.PartDestroyed,
                    TargetSide = BattleSide.Enemy,
                    TargetIndex = enemy.Index,
                    PartIndex = part.Index,
                    Message = $"{enemy.Name}의 {part.Name} 파괴!",
                });
            }
            if (!enemy.IsAlive && dealt > 0)
                events.Add(DownEvent(enemy));
            return dealt;
        }

        // ───────────────────────── 적 행동 ─────────────────────────

        private void ResolveEnemyPhase(List<BattleEvent> events)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive)
                    continue;

                foreach (var intent in enemy.Intents)
                {
                    if (arthurs.All(a => !a.IsAlive))
                        return;

                    var action = intent.Action;
                    var source = enemy.Part(action.SourcePart);
                    string cancelReason = enemy.StunTurns > 0 ? "행동 정지"
                        : source != null && source.IsDestroyed ? $"{source.Name} 파괴"
                        : null;
                    if (cancelReason != null)
                    {
                        events.Add(new BattleEvent
                        {
                            Type = BattleEventType.EnemyActionCanceled,
                            ActorSide = BattleSide.Enemy,
                            ActorIndex = enemy.Index,
                            PartIndex = action.SourcePart,
                            Message = $"{enemy.Name}: {cancelReason}로 「{action.Name}」 취소",
                        });
                        continue;
                    }

                    events.Add(new BattleEvent
                    {
                        Type = BattleEventType.EnemyAction,
                        ActorSide = BattleSide.Enemy,
                        ActorIndex = enemy.Index,
                        PartIndex = action.SourcePart,
                        Message = $"{enemy.Name}: 「{action.Name}」",
                    });

                    if (action.Kind == EnemyActionKind.Attack)
                        ExecuteEnemyAttack(enemy, intent, events);
                    else
                        ExecuteEnemyPowerUp(enemy, action, events);
                }
            }
        }

        private void ExecuteEnemyAttack(Enemy enemy, EnemyIntent intent, List<BattleEvent> events)
        {
            var action = intent.Action;
            IEnumerable<Arthur> targets;
            if (action.TargetsAll)
            {
                targets = arthurs.Where(a => a.IsAlive).ToList();
            }
            else
            {
                // 예고한 대상이 그사이 쓰러졌으면 다른 아서를 노린다.
                var target = ArthurAt(intent.TargetArthurIndex);
                targets = new[] { target != null && target.IsAlive ? target : RandomAliveArthur() };
            }

            foreach (var arthur in targets)
            {
                int power = action.Power
                    + enemy.StatusTotal(EffectKind.DamageUp, action.Stat)
                    - enemy.StatusTotal(EffectKind.DamageDown, action.Stat);
                int damage = Math.Max(power - arthur.Defense(action.Stat), 1);
                arthur.TakeDamage(damage);
                events.Add(new BattleEvent
                {
                    Type = BattleEventType.Damage,
                    ActorSide = BattleSide.Enemy,
                    ActorIndex = enemy.Index,
                    TargetSide = BattleSide.Ally,
                    TargetIndex = arthur.Index,
                    PartIndex = action.SourcePart,
                    Element = enemy.Element,
                    Value = damage,
                    Message = $"{arthur.Name}에게 {damage} 피해",
                });
                if (!arthur.IsAlive)
                    events.Add(DownEvent(arthur));
            }
        }

        private void ExecuteEnemyPowerUp(Enemy enemy, EnemyAction action, List<BattleEvent> events)
        {
            var targets = action.TargetsAll ? enemies.Where(e => e.IsAlive).ToList() : new List<Enemy> { enemy };
            foreach (var target in targets)
                ApplyStatus(events, enemy, target, new StatusEffect(EffectKind.DamageUp, action.Stat, action.Power, action.Turns));
        }

        // ───────────────────────── 턴 끝 ─────────────────────────

        /// <summary>독과 지속 회복이 돌고, 모든 지속 효과의 남은 턴이 줄어든다.</summary>
        private void EndTurn(List<BattleEvent> events)
        {
            var units = arthurs.Cast<BattleUnit>().Concat(enemies).ToList();
            foreach (var unit in units)
            {
                foreach (var status in unit.Statuses.ToList())
                {
                    if (!unit.IsAlive)
                        break;

                    if (status.Kind == EffectKind.Poison)
                    {
                        int dealt = unit.TakeDamage(status.Value);
                        events.Add(TickEvent(BattleEventType.PoisonTick, unit, dealt, $"{unit.Name}: 독 {dealt}"));
                        if (!unit.IsAlive)
                            events.Add(DownEvent(unit));
                    }
                    else if (status.Kind == EffectKind.HealOverTime)
                    {
                        int healed = unit.Heal(status.Value);
                        events.Add(TickEvent(BattleEventType.HealTick, unit, healed, $"{unit.Name}: 지속 회복 {healed}"));
                    }
                }
            }

            foreach (var unit in units)
                unit.TickStatuses();
            foreach (var enemy in enemies.Where(e => e.StunTurns > 0))
                enemy.StunTurns--;
        }

        /// <summary>승패가 정해졌으면 true.</summary>
        private bool UpdateOutcome(List<BattleEvent> events)
        {
            if (Outcome == BattleOutcome.Ongoing)
            {
                if (enemies.All(e => !e.IsAlive))
                    Outcome = BattleOutcome.Victory;
                else if (arthurs.All(a => !a.IsAlive))
                    Outcome = BattleOutcome.Defeat;

                if (Outcome != BattleOutcome.Ongoing)
                {
                    events.Add(new BattleEvent
                    {
                        Type = BattleEventType.BattleEnded,
                        Outcome = Outcome,
                        Message = Outcome == BattleOutcome.Victory ? "승리!" : "패배...",
                    });
                }
            }
            return Outcome != BattleOutcome.Ongoing;
        }

        // ───────────────────────── 대상 찾기 ─────────────────────────

        private Arthur ArthurAt(int index) => index >= 0 && index < arthurs.Count ? arthurs[index] : null;

        private Enemy EnemyAt(int index) => index >= 0 && index < enemies.Count ? enemies[index] : null;

        private Arthur RandomAliveArthur()
        {
            var alive = arthurs.Where(a => a.IsAlive).ToList();
            return alive[random.Next(alive.Count)];
        }

        /// <summary>고른 적이 그사이 쓰러졌으면 살아 있는 첫 번째 적을 노린다.</summary>
        private Enemy ResolveEnemy(TargetRef target)
        {
            var enemy = target.Side == BattleSide.Enemy ? EnemyAt(target.Index) : null;
            return enemy != null && enemy.IsAlive ? enemy : enemies.FirstOrDefault(e => e.IsAlive);
        }

        private IEnumerable<(Enemy, EnemyPart)> DamageTargets(EffectTarget target, TargetRef chosen)
        {
            if (target == EffectTarget.AllEnemies)
                return enemies.Where(e => e.IsAlive).Select(e => (e, (EnemyPart)null)).ToList();

            var enemy = ResolveEnemy(chosen);
            if (enemy == null)
                return Array.Empty<(Enemy, EnemyPart)>();

            // 이미 파괴된 부위를 노렸으면 본체를 친다.
            var part = enemy.Index == chosen.Index ? enemy.Part(chosen.Part) : null;
            if (part != null && part.IsDestroyed)
                part = null;
            return new[] { (enemy, part) };
        }

        private IEnumerable<Enemy> EnemyTargets(EffectTarget target, TargetRef chosen)
        {
            if (target == EffectTarget.AllEnemies)
                return enemies.Where(e => e.IsAlive).ToList();
            var enemy = ResolveEnemy(chosen);
            return enemy == null ? Array.Empty<Enemy>() : new[] { enemy };
        }

        private IEnumerable<Arthur> AllyTargets(EffectTarget target, PlayContext context)
        {
            switch (target)
            {
                case EffectTarget.AllAllies:
                    return arthurs.Where(a => a.IsAlive).ToList();
                case EffectTarget.AllySingle:
                {
                    var ally = context.Target.Side == BattleSide.Ally ? ArthurAt(context.Target.Index) : null;
                    return new[] { ally != null && ally.IsAlive ? ally : context.Arthur };
                }
                default:
                    return new[] { context.Arthur };
            }
        }

        // ───────────────────────── 이벤트 만들기 ─────────────────────────

        private static int Scale(double value, double multiplier)
        {
            return (int)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
        }

        private static void ApplyStatus(List<BattleEvent> events, BattleUnit actor, BattleUnit target, StatusEffect status)
        {
            target.AddStatus(status);
            events.Add(new BattleEvent
            {
                Type = BattleEventType.StatusApplied,
                ActorSide = actor.Side,
                ActorIndex = actor.Index,
                TargetSide = target.Side,
                TargetIndex = target.Index,
                EffectKind = status.Kind,
                Value = status.Value,
                Message = $"{target.Name}: {status.Kind} {status.Value} ({status.TurnsLeft}턴)",
            });
        }

        private static void AddHealEvent(List<BattleEvent> events, BattleEventType type, Arthur actor, Arthur target, int healed)
        {
            events.Add(new BattleEvent
            {
                Type = type,
                ActorSide = BattleSide.Ally,
                ActorIndex = actor.Index,
                TargetSide = BattleSide.Ally,
                TargetIndex = target.Index,
                Value = healed,
                Message = $"{target.Name}: HP {healed} 회복",
            });
        }

        private static BattleEvent TickEvent(BattleEventType type, BattleUnit unit, int value, string message)
        {
            return new BattleEvent { Type = type, TargetSide = unit.Side, TargetIndex = unit.Index, Value = value, Message = message };
        }

        private static BattleEvent DownEvent(BattleUnit unit)
        {
            return new BattleEvent
            {
                Type = unit.Side == BattleSide.Enemy ? BattleEventType.EnemyDefeated : BattleEventType.ArthurDown,
                TargetSide = unit.Side,
                TargetIndex = unit.Index,
                Message = unit.Side == BattleSide.Enemy ? $"{unit.Name} 쓰러뜨림!" : $"{unit.Name} 쓰러짐",
            };
        }
    }
}
