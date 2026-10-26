using System.Linq;
using ChainKnights.Core.Cards;
using NUnit.Framework;

namespace ChainKnights.Core.Tests
{
    // UnityEngine을 쓰지 않으므로 Unity Test Runner와 Tools/CoreTests(dotnet test) 양쪽에서 돈다.
    public class CardCatalogTests
    {
        [Test]
        public void Catalog_Has84LaunchCards_WithUniqueIds()
        {
            Assert.AreEqual(84, CardCatalog.All.Count);
            Assert.AreEqual(CardCatalog.All.Count, CardCatalog.All.Select(c => c.Id).Distinct().Count());
        }

        [Test]
        public void Catalog_HasUniqueNames_SoAnyCardCanShareADeck()
        {
            Assert.AreEqual(CardCatalog.All.Count, CardCatalog.All.Select(c => c.NameKo).Distinct().Count());
        }

        [Test]
        public void EveryCard_HasValidCostRarityAndSkills()
        {
            foreach (var card in CardCatalog.All)
            {
                Assert.That(card.Cost, Is.InRange(1, 5), card.NameKo);
                Assert.That(card.Rarity, Is.InRange(1, 5), card.NameKo);
                Assert.That(card.Normal.Effects, Is.Not.Empty, card.NameKo);
                Assert.AreEqual(card.Job == Job.None, card.Awakened == null, card.NameKo);

                var effects = card.Normal.Effects.Concat(card.Awakened?.Effects ?? Enumerable.Empty<SkillEffect>());
                foreach (var effect in effects)
                    Assert.That(effect.MaxValue, Is.GreaterThanOrEqualTo(effect.BaseValue), card.NameKo);
            }
        }

        [Test]
        public void StarterCards_AreEnoughToBuildA10CardDeck()
        {
            var starters = CardCatalog.StarterCards.ToList();

            Assert.AreEqual(28, starters.Count);
            Assert.That(starters.All(c => c.Rarity <= 2));
            Assert.AreEqual(3, starters.Count(c => c.Job == Job.None));
        }

        [Test]
        public void IceDragon_DropsOlwenAndSigrun()
        {
            var raidDrops = CardCatalog.All.Where(c => c.Source == CardSource.RaidDrop).Select(c => c.Id);

            CollectionAssert.AreEquivalent(new[] { 69, 75 }, raidDrops);
        }

        [Test]
        public void MaxLevel_IsTenPerStar()
        {
            Assert.AreEqual(10, CardCatalog.Get(1).MaxLevel);
            Assert.AreEqual(50, CardCatalog.Get(37).MaxLevel);
        }

        [Test]
        public void ValueAt_InterpolatesBetweenLevelOneAndMaxLevel()
        {
            var card = CardCatalog.Get(3); // 제2형 가헤리스: ★2, 507 → 830
            var damage = card.Normal.Effects[0];

            Assert.AreEqual(507, damage.ValueAt(1, card.MaxLevel));
            Assert.AreEqual(660, damage.ValueAt(10, card.MaxLevel));
            Assert.AreEqual(830, damage.ValueAt(card.MaxLevel, card.MaxLevel));
            Assert.AreEqual(830, damage.ValueAt(99, card.MaxLevel));
        }

        [Test]
        public void SkillFor_UsesAwakenedSkill_OnlyForMatchingJob()
        {
            var card = CardCatalog.Get(37); // 허무형 이터널 플레임: 전사

            Assert.AreSame(card.Awakened, card.SkillFor(Job.Warrior));
            Assert.AreSame(card.Normal, card.SkillFor(Job.Priest));
            Assert.AreEqual(EffectTarget.AllEnemies, card.Awakened.Effects[0].Target);
        }

        [Test]
        public void SharedCards_NeverAwaken()
        {
            var golem = CardCatalog.Get(221);

            Assert.IsFalse(golem.IsAwakenedFor(Job.None));
            Assert.AreSame(golem.Normal, golem.SkillFor(Job.Warrior));
        }
    }
}
