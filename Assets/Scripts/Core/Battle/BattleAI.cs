using System;
using System.Collections.Generic;
using System.Linq;
using ChainKnights.Core.Cards;

namespace ChainKnights.Core.Battle
{
    /// <summary>
    /// 솔로 모드의 AI 동료. 공개된 정보(적, 아군 HP, 자기 손패)만 보고 고른다.
    /// 다른 아군이 무엇을 냈는지는 사람 플레이어처럼 모른다.
    /// </summary>
    public static class BattleAI
    {
        public static IReadOnlyList<CardPlay> ChoosePlays(BattleSession session, int arthurIndex)
        {
            var arthur = session.Arthurs[arthurIndex];
            if (!arthur.IsAlive)
                return Array.Empty<CardPlay>();

            var enemyTarget = ChooseEnemyTarget(session);
            var allyTarget = session.Arthurs.Where(a => a.IsAlive).OrderBy(a => a.Hp / (double)a.MaxHp).First();

            var scored = arthur.Hand
                .Select(card => (card, score: Score(session, arthur, card)))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score / x.card.Card.Cost)
                .ToList();

            var plays = new List<CardPlay>();
            int budget = session.Cost;
            foreach (var (card, _) in scored)
            {
                if (card.Card.Cost > budget)
                    continue;
                budget -= card.Card.Cost;

                var skill = card.Card.SkillFor(arthur.Job);
                var target = skill.Effects.Any(e => e.Target == EffectTarget.EnemySingle) ? enemyTarget
                    : skill.Effects.Any(e => e.Target == EffectTarget.AllySingle) ? TargetRef.Ally(allyTarget.Index)
                    : TargetRef.None;
                plays.Add(new CardPlay(card.InstanceId, target));
            }
            return plays;
        }

        /// <summary>남은 부위가 있으면 부위를 노린다 (부위를 부수면 그 부위의 공격이 사라진다). 없으면 HP가 가장 낮은 적.</summary>
        private static TargetRef ChooseEnemyTarget(BattleSession session)
        {
            var alive = session.Enemies.Where(e => e.IsAlive).ToList();
            foreach (var enemy in alive)
            {
                var part = enemy.Parts.FirstOrDefault(p => !p.IsDestroyed);
                if (part != null)
                    return TargetRef.Enemy(enemy.Index, part.Index);
            }
            var weakest = alive.OrderBy(e => e.Hp).First();
            return TargetRef.Enemy(weakest.Index);
        }

        private static double Score(BattleSession session, Arthur arthur, CardInstance card)
        {
            var skill = card.Card.SkillFor(arthur.Job);
            int aliveEnemies = session.Enemies.Count(e => e.IsAlive);
            bool allyInDanger = session.Arthurs.Any(a => a.IsAlive && a.Hp < a.MaxHp * 0.5);
            bool allyHurt = session.Arthurs.Any(a => a.IsAlive && a.Hp < a.MaxHp * 0.85);

            double score = 0;
            foreach (var effect in skill.Effects)
            {
                double value = card.ValueOf(effect);
                switch (effect.Kind)
                {
                    case EffectKind.Damage:
                        score += value * (effect.Target == EffectTarget.AllEnemies ? aliveEnemies : 1);
                        break;
                    case EffectKind.Heal:
                    case EffectKind.HealOverTime:
                        score += value * (allyInDanger ? 2.0 : allyHurt ? 0.6 : 0.05) * Math.Max(1, effect.Turns);
                        break;
                    case EffectKind.DamageUp:
                    case EffectKind.DefenseUp:
                    case EffectKind.DamageDown:
                    case EffectKind.DefenseDown:
                        score += value * 0.8 * Math.Max(1, effect.Turns) * (effect.Target == EffectTarget.AllAllies ? 2 : 1);
                        break;
                    case EffectKind.Poison:
                        score += value * effect.Turns * 0.7;
                        break;
                    case EffectKind.Draw:
                        score += 150 * effect.BaseValue;
                        break;
                    default:
                        score += 50;
                        break;
                }
            }
            return score;
        }
    }
}
