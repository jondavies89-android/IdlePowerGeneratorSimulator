using System.Linq;
using NUnit.Framework;

namespace IdlePowerGen.Tests
{
    public class Era1CatalogueTests : Era1SceneFixture
    {
        [Test]
        public void GameManagerHasAllNineEra1Upgrades() =>
            Assert.That(Managers.GameManager.Instance.UpgradeCatalogue.Count, Is.EqualTo(9));

        [Test]
        public void GameManagerHasAllThreeEra1Generators() =>
            Assert.That(Managers.GameManager.Instance.GeneratorCatalogue.Count, Is.EqualTo(3));

        [Test]
        public void Era1ConfigIsWired() =>
            Assert.That(Managers.GameManager.Instance.GetEraConfig(1)?.eraName, Is.EqualTo("The Workshop"));

        [Test]
        public void EveryGeneratorIsGatedByAnUpgradeInTheCatalogue()
        {
            var gm  = Managers.GameManager.Instance;
            var ids = gm.UpgradeCatalogue.Select(u => u.upgradeId).ToHashSet();
            Assert.That(gm.GeneratorCatalogue.All(g => ids.Contains(g.requiredUpgradeId)), Is.True);
        }
    }
}
