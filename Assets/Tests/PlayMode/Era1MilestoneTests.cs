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

        // The whole end of the era: knock, 1 kW, accept, an hour's supply, a shilling, Era 2.
        [UnityTest]
        public IEnumerator SupplyingTheNeighbourForAnHourAdvancesToEra2()
        {
            GM.Ledger.AddJoules(500);
            // Water wheel and gearing make 65 W; four magnets make it 1040 W.
            SetLevels((Data.Era1Data.BetterCrankId, 1), (Data.Era1Data.IronFlywheelId, 1),
                      (Data.Era1Data.MagnetsId, 4), (Data.Era1Data.WaterWheelId, 1),
                      (Data.Era1Data.BeltsGearingId, 4));
            yield return null;

            var customer = Object.FindAnyObjectByType<Controllers.Era1FirstCustomerController>();
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
