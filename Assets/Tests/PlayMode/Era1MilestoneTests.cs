using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace IdlePowerGen.Tests
{
    public class Era1MilestoneTests : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static UI.ToastView Toast => Object.FindAnyObjectByType<UI.ToastView>();

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator FiftyJoulesShowsFirstMachineToast()
        {
            GM.Ledger.AddJoules(50);
            yield return null;
            Assert.That(Toast.CurrentText, Is.EqualTo("Your first machine hums to life."));
        }

        [UnityTest]
        public IEnumerator FiveHundredJoulesShowsNeighbourToast()
        {
            var seen = new List<string>();
            void Record(MilestoneReachedEvent e) => seen.Add(e.Id);
            Utils.EventBus.Subscribe<MilestoneReachedEvent>(Record);
            GM.Ledger.AddJoules(500);
            yield return null;
            Utils.EventBus.Unsubscribe<MilestoneReachedEvent>(Record);
            Assert.That(seen, Does.Contain(Data.Era1Data.FirstCustomerMilestoneId));
        }

        [UnityTest]
        public IEnumerator MilestoneFiresOnlyOnce()
        {
            int count = 0;
            void Count(MilestoneReachedEvent e) { if (e.Id == Data.Era1Data.FirstMachineMilestoneId) count++; }
            Utils.EventBus.Subscribe<MilestoneReachedEvent>(Count);
            GM.Ledger.AddJoules(50);
            GM.Ledger.SpendJoules(50);
            GM.Ledger.AddJoules(50);
            yield return null;
            Utils.EventBus.Unsubscribe<MilestoneReachedEvent>(Count);
            Assert.That(count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReachedMilestonesSurviveSaveAndLoad()
        {
            GM.Ledger.AddJoules(50);
            Save.SaveManager.Instance.Save();
            Managers.Era1MilestoneTracker.Instance.LoadFromSave(null);
            Save.SaveManager.Instance.Load();
            yield return null;
            Assert.That(Managers.Era1MilestoneTracker.Instance.HasReached(Data.Era1Data.FirstMachineMilestoneId), Is.True);
        }

        [Test]
        public void V1SaveMigratesWithPassedMilestonesMarkedReached()
        {
            var v1 = new Save.SaveData { version = 1, joules = 600, reachedMilestones = null };
            Save.SaveManager.Migrate(v1);
            Assert.That(v1.reachedMilestones, Is.EquivalentTo(new[]
                { Data.Era1Data.FirstMachineMilestoneId, Data.Era1Data.FirstCustomerMilestoneId }));
        }

        [UnityTest]
        public IEnumerator FirstCustomerCannotBeAcceptedBelowOneKilowatt()
        {
            GM.Ledger.AddJoules(500);
            yield return null;
            var customer = Object.FindAnyObjectByType<Controllers.Era1FirstCustomerController>();
            Assert.That(customer.Accept(), Is.False);
        }

        private static Controllers.Era1FirstCustomerController Customer =>
            Object.FindAnyObjectByType<Controllers.Era1FirstCustomerController>();

        // The neighbour has knocked and the water wheel turns with this many gearing levels.
        // Waits out a tick, since the contract goes by each tick's output.
        private static IEnumerator KnockedWithGearing(int gearing)
        {
            GM.Ledger.AddJoules(500);
            SetLevels((Data.Era1Data.BetterCrankId, 1), (Data.Era1Data.IronFlywheelId, 1),
                      (Data.Era1Data.WaterWheelId, 1), (Data.Era1Data.BeltsGearingId, gearing));
            yield return new WaitForSeconds(1.1f);
        }

        [UnityTest]
        public IEnumerator NeighbourCardFillsTowardsTheWattageHeNeeds()
        {
            yield return KnockedWithGearing(2); // 200 W of his 1 kW
            yield return new WaitForSeconds(0.3f); // the bar eases
            var fill = GameObject.Find("FirstCustomerCard").transform.Find("Progress/Fill")
                                 .GetComponent<UnityEngine.UI.Image>();
            Assert.That(fill.fillAmount, Is.EqualTo(0.2f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator DippingBelowOneKilowattCallsTheContractOff()
        {
            yield return KnockedWithGearing(5); // 1.6 kW
            Customer.Accept();
            SetLevels((Data.Era1Data.BetterCrankId, 1), (Data.Era1Data.IronFlywheelId, 1),
                      (Data.Era1Data.WaterWheelId, 1)); // back to 50 W
            yield return new WaitForSeconds(1.1f);
            Assert.That(Customer.Current, Is.EqualTo(Controllers.Era1FirstCustomerController.Stage.Offered));
        }

        [UnityTest]
        public IEnumerator SupplyingTheNeighbourBanksOnlyWhatIsLeftOver()
        {
            yield return KnockedWithGearing(5); // 1.6 kW
            Customer.Accept();
            yield return new WaitForSeconds(1.1f); // the first tick of the supply
            double before = GM.Ledger.Joules;
            yield return new WaitForSeconds(1f);
            Assert.That(GM.Ledger.Joules - before, Is.EqualTo(600).Within(1));
        }

        // The whole end of the era: knock, 1 kW, accept, an hour's supply, a shilling, Era 2.
        [UnityTest]
        public IEnumerator SupplyingTheNeighbourForAnHourAdvancesToEra2()
        {
            yield return KnockedWithGearing(5); // five doublings of 50 W: 1.6 kW, untended

            var customer = Customer;
            Assert.That(customer.Accept(), Is.True, "Accept failed at 1 kW");

            // One game hour is 60 real seconds; run time 20× faster.
            Time.timeScale = 20f;
            float deadline = Time.realtimeSinceStartup + 15f;
            while (Managers.EraManager.Instance.CurrentEra < 2f && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(Managers.EraManager.Instance.CurrentEra, Is.EqualTo(2f));
        }
    }
}
