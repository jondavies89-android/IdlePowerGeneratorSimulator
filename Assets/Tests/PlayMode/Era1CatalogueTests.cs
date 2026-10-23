using System.Linq;
using NUnit.Framework;

namespace IdlePowerGen.Tests
{
    public class Era1CatalogueTests : Era1SceneFixture
    {
        [Test]
        public void GameManagerHasAllSixEra1Upgrades() =>
            Assert.That(Managers.GameManager.Instance.UpgradeCatalogue.Count, Is.EqualTo(6));

        [Test]
        public void Era1HasNoStackableMachines() =>
            Assert.That(Managers.GameManager.Instance.GeneratorCatalogue.Count, Is.EqualTo(0));

        [Test]
        public void Era1ConfigIsWired() =>
            Assert.That(Managers.GameManager.Instance.GetEraConfig(1)?.eraName, Is.EqualTo("The Workshop"));

        [Test]
        public void EveryPrerequisiteIsAnUpgradeInTheCatalogue()
        {
            var gm  = Managers.GameManager.Instance;
            var ids = gm.UpgradeCatalogue.Select(u => u.upgradeId).ToHashSet();
            Assert.That(gm.UpgradeCatalogue.SelectMany(u => u.requiredUpgradeIds).All(ids.Contains), Is.True);
        }
    }
}
