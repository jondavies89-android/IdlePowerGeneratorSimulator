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
            GM.Ledger.AddJoules(5);
            yield return null;
            FindButton("Upgrade_era1_better_crank").onClick.Invoke();
            Assert.That(GM.Upgrades.IsPurchased("era1_better_crank"), Is.True);
        }

        [UnityTest]
        public IEnumerator PurchasedCardIsHidden()
        {
            GM.Ledger.AddJoules(5);
            yield return null;
            var card = FindButton("Upgrade_era1_better_crank");
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
        public IEnumerator SpinningJennyRowAppearsOnlyAfterItsPatent()
        {
            var row = GameObject.Find("Generator_SpinningJenny");
            Assert.That(row, Is.Null, "Row should be hidden before the patent");

            GM.Ledger.AddJoules(5 + 30);
            FindButton("Upgrade_era1_better_crank").onClick.Invoke();
            yield return null;
            FindButton("Upgrade_era1_unlock_spinning_jenny").onClick.Invoke();
            yield return null;
            Assert.That(GameObject.Find("Generator_SpinningJenny"), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator BuyingASpinningJennyFromItsRowCostsFiftyJoules()
        {
            GM.Ledger.AddJoules(5 + 30 + 50);
            FindButton("Upgrade_era1_better_crank").onClick.Invoke();
            yield return null;
            FindButton("Upgrade_era1_unlock_spinning_jenny").onClick.Invoke();
            yield return null;
            FindButton("Generator_SpinningJenny").onClick.Invoke();
            Assert.That(GM.Ledger.Joules, Is.EqualTo(0.0));
        }
    }
}
