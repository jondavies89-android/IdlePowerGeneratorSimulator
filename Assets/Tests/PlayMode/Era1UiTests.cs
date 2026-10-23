using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace IdlePowerGen.Tests
{
    // Drives the real scene through its buttons, as a player would.
    public class Era1UiTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;

        private static Button FindButton(string name)
        {
            var go = GameObject.Find(name);
            Assert.That(go, Is.Not.Null, $"No GameObject named {name}");
            return go.GetComponent<Button>();
        }

        [UnityTest]
        public IEnumerator CrankButtonAddsOneJoule()
        {
            FindButton("CrankButton").onClick.Invoke();
            yield return null;
            Assert.That(GM.Ledger.Joules, Is.EqualTo(1.0));
        }

        [UnityTest]
        public IEnumerator BetterCrankCardPurchasesTheUpgrade()
        {
            GM.Ledger.AddJoules(15);
            yield return null;
            FindButton("Upgrade_era1_better_crank").onClick.Invoke();
            Assert.That(GM.Upgrades.IsPurchased("era1_better_crank"), Is.True);
        }

        [UnityTest]
        public IEnumerator LevelledCardStaysAfterPurchase()
        {
            GM.Ledger.AddJoules(15);
            yield return null;
            var card = FindButton("Upgrade_era1_better_crank");
            card.onClick.Invoke();
            yield return null;
            Assert.That(card.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator LevelledCardShowsTheNextLevelsCost()
        {
            GM.Ledger.AddJoules(15);
            yield return null;
            var card = FindButton("Upgrade_era1_better_crank");
            card.onClick.Invoke();
            yield return null;
            Assert.That(card.GetComponent<UI.UpgradeCardView>().CostText,
                        Is.EqualTo(Utils.NumberFormatter.FormatJoules(22.5)));
        }

        [UnityTest]
        public IEnumerator OneTimeCardIsHiddenOncePurchased()
        {
            SetLevels(("era1_better_crank", 1), ("era1_iron_flywheel", 1));
            GM.Ledger.AddJoules(5_000);
            yield return null;
            var card = FindButton("Upgrade_era1_water_wheel");
            card.onClick.Invoke();
            yield return null;
            Assert.That(card.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator UnaffordableCardIsNotInteractable()
        {
            yield return null;
            Assert.That(FindButton("Upgrade_era1_better_crank").interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator BearingsCardAppearsOnceTheFlywheelIsBought()
        {
            SetLevels(("era1_better_crank", 1), ("era1_iron_flywheel", 1));
            yield return null;
            Assert.That(GameObject.Find("Upgrade_era1_bearings"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BearingsCardIsHiddenBeforeTheFlywheel()
        {
            yield return null;
            Assert.That(GameObject.Find("Upgrade_era1_bearings"), Is.Null);
        }

        [UnityTest]
        public IEnumerator Era1ShowsNoMachinesSection()
        {
            yield return null;
            Assert.That(GameObject.Find("MachinesHeader"), Is.Null);
        }
    }
}
