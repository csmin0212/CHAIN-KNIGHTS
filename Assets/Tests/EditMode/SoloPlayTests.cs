using System.Linq;
using ChainKnights.Core.Battle;
using ChainKnights.Core.Cards;
using NUnit.Framework;

namespace ChainKnights.Core.Tests
{
    public class SoloPlayTests
    {
        private static readonly Job[] AllJobs = { Job.Warrior, Job.Guardian, Job.Mage, Job.Priest };

        private static BattleSession AutoParty(int seed, params EnemySetup[] enemies)
        {
            return new BattleSession(new BattleSetup
            {
                Arthurs = AllJobs.Select(job => new ArthurSetup
                {
                    Name = CardText.JobName(job),
                    Job = job,
                    MaxHp = 6000,
                    Deck = StarterDecks.For(job, maxLevel: true),
                }).ToList(),
                Enemies = enemies,
                Random = new SeededBattleRandom(seed),
            });
        }

        [Test]
        public void StarterDecks_HaveTenUniqueCards_LedByTheJobsOwnCards()
        {
            foreach (var job in AllJobs)
            {
                var deck = StarterDecks.For(job);
                var cards = deck.Select(d => CardCatalog.Get(d.CardId)).ToList();

                Assert.AreEqual(BattleRules.DeckSize, cards.Count, CardText.JobName(job));
                Assert.AreEqual(cards.Count, cards.Select(c => c.NameKo).Distinct().Count(), CardText.JobName(job));
                Assert.AreEqual(job, cards[0].Job, CardText.JobName(job));
            }
        }

        [Test]
        public void Glacies_HasAClawOnMedium_AndAWingFromHard()
        {
            Assert.AreEqual(1, Encounters.Glacies(RaidDifficulty.Medium).Parts.Count);
            Assert.AreEqual(2, Encounters.Glacies(RaidDifficulty.Hard).Parts.Count);
            Assert.AreEqual(40000, Encounters.Glacies(RaidDifficulty.Medium).MaxHp);
        }

        [Test]
        public void Glacies_BreathesEveryThirdTurn()
        {
            var battle = AutoParty(1, Encounters.Glacies(RaidDifficulty.Medium));

            battle.StartTurn();
            Assert.IsFalse(battle.Enemies[0].Intents.Any(i => i.Action.TargetsAll));
            battle.ResolveTurn();
            battle.StartTurn();
            battle.ResolveTurn();
            battle.StartTurn();
            Assert.IsTrue(battle.Enemies[0].Intents.Any(i => i.Action.Name == "빙결 브레스"));
        }

        [Test]
        public void AI_PlaysValidCardsWithinCost()
        {
            var battle = AutoParty(7, Encounters.GolemPack().ToArray());
            battle.StartTurn();

            for (int i = 0; i < battle.Arthurs.Count; i++)
            {
                var plays = BattleAI.ChoosePlays(battle, i);
                var result = battle.Submit(i, plays);
                Assert.IsTrue(result.Success, result.Error);
                Assert.That(plays.Sum(p => battle.Arthurs[i].FindInHand(p.InstanceId).Card.Cost), Is.LessThanOrEqualTo(battle.Cost));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void FourAIs_FinishTheGolemPack(int seed)
        {
            var battle = AutoParty(seed, Encounters.GolemPack().ToArray());

            for (int turn = 0; turn < 30 && battle.Outcome == BattleOutcome.Ongoing; turn++)
            {
                battle.StartTurn();
                for (int i = 0; i < battle.Arthurs.Count; i++)
                {
                    if (battle.Arthurs[i].IsAlive)
                        battle.Submit(i, BattleAI.ChoosePlays(battle, i));
                }
                battle.ResolveTurn();
            }

            Assert.AreEqual(BattleOutcome.Victory, battle.Outcome);
        }
    }
}
