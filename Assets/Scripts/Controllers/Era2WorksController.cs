using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // The running of the Era 2 works: the contract board and its bookings, wages, the coal
    // hopper, and the clerk and coal merchant once they are hired. Ticks with the sim.
    public class Era2WorksController : MonoBehaviour
    {
        [SerializeField] private Era2GeneratorController _generators;

        public Economy.ContractBoard  Board  { get; } = new();
        public Economy.Era2CoalHopper Hopper { get; } = new();

        // Game day Era 2 began on: the date shown counts on from 9 October 1851 there.
        public long StartDay { get; private set; }

        private bool _toldUnpaid;

        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Simulation.SimulationTick Sim => Simulation.SimulationTick.Instance;

        public static bool IsEra2 =>
            Managers.EraManager.Instance != null && Managers.EraManager.Instance.CurrentEra >= 2f;

        public static double Now => GameClock.Instance != null ? GameClock.Instance.GameTimeSeconds : 0;

        private void Awake()
        {
            EventBus.Subscribe<SimTickEvent>(OnSimTick);
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<SimTickEvent>(OnSimTick);
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        // ---- Era start ----

        private void OnEraAdvanced(EraAdvancedEvent e)
        {
            if (e.ToEra != 2) return;
            // Forward to the next evening, never back: bookings are kept by game day.
            double now = Now;
            long day = Economy.ContractBoard.DayOf(now) + 1;
            GameClock.Instance?.SetGameTime(day * Economy.ContractBoard.SecondsPerDay + Data.Era2Data.StartHour * 3600.0);
            StartDay = day;
            Hopper.Fill(CoalBurnCapacityBasis);
        }

        public DateTime Date =>
            new DateTime(Data.Era2Data.StartYear, Data.Era2Data.StartMonth, Data.Era2Data.StartDay)
                .AddDays(Math.Max(0, Economy.ContractBoard.DayOf(Now) - StartDay));

        // ---- Contracts ----

        public IEnumerable<ContractDefinition> Era2Contracts =>
            GM == null ? Enumerable.Empty<ContractDefinition>()
                       : GM.ContractCatalogue.Where(c => c != null && c.minEra == Data.Era2Data.EraNumber);

        public int Slots
        {
            get
            {
                int slots = Data.Era2Data.BaseContractSlots;
                var gm = GM;
                if (gm == null) return slots;
                foreach (var def in gm.UpgradeCatalogue)
                    if (def.effect == UpgradeEffect.AddContractSlot)
                        slots += (int)Math.Round(def.effectValue) * gm.Upgrades.Level(def.upgradeId);
                return slots;
            }
        }

        public IEnumerable<Economy.ContractInstance> Engagements =>
            GM == null ? Enumerable.Empty<Economy.ContractInstance>()
                       : GM.Contracts.ActiveContracts.Where(c => c.Definition.minEra == Data.Era2Data.EraNumber);

        // Watts the works has promised to customers right now.
        public double CommittedWatts => Engagements.Sum(c => c.Definition.requiredWatts);

        // Output on the last tick: what the works actually made.
        public static double CapacityWatts => Sim != null ? Sim.Grid.TotalOutputWatts : 0;

        public bool IsOffered(ContractDefinition def) =>
            IsEra2 && GM != null && Board.IsOffered(def, Now, GM.Ledger.Reputation, GM.Contracts.ActiveContracts);

        public bool HasFreeSlot => GM != null && GM.Contracts.ActiveContracts.Count < Slots;

        public bool Book(ContractDefinition def) =>
            IsEra2 && GM != null && Board.TryBook(def, Now, GM.Ledger.Reputation, GM.Contracts, Slots);

        public static decimal Pay(ContractDefinition def) =>
            GM == null ? 0m
                       : (decimal)(def.durationHours * def.requiredWatts * GM.ContractBaseRate(def) * def.payRateMultiplier);

        // ---- Running costs ----

        private IEnumerable<Simulation.GeneratorInstance> Fleet =>
            Sim != null ? Sim.Grid.Generators : Enumerable.Empty<Simulation.GeneratorInstance>();

        // Pennies an hour of wages for the hands at work.
        public double WagesPerHour => Fleet.Where(g => g.IsOnline).Sum(g => g.Definition.wagePenniesPerHour);

        // Coal an hour for the engines running.
        public double CoalBurnPerHour => Fleet.Where(g => g.IsOnline).Sum(g => g.Definition.coalPenniesPerHour);

        // The hopper is sized to every engine owned, running or stopped, so an empty hopper
        // doesn't shrink to match the engines it has just stopped.
        public double CoalBurnCapacityBasis => Fleet.Sum(g => g.Definition.coalPenniesPerHour);

        public bool HasCoalEngines => CoalBurnCapacityBasis > 0;

        public decimal CoalOrderCost => Hopper.OrderCost(CoalBurnCapacityBasis);

        public bool OrderCoal()
        {
            if (GM == null || !Hopper.Order(CoalBurnCapacityBasis, GM.Ledger)) return false;
            Restart(g => g.Definition.coalPenniesPerHour > 0);
            return true;
        }

        public bool ApprenticesUnpaid { get; private set; }

        private static bool Unlocked(string featureId) =>
            Managers.UpgradeEffectApplier.Instance != null &&
            Managers.UpgradeEffectApplier.Instance.IsFeatureUnlocked(featureId);

        private void OnSimTick(SimTickEvent e)
        {
            if (!IsEra2 || GM == null) return;
            double scale = GameClock.Instance != null ? GameClock.Instance.GameTimeScale : 1f;
            double hours = e.DeltaTime * scale / 3600.0;

            PayWages(hours);
            BurnCoal(hours);
            if (Unlocked(Data.Era2Data.ClerkFeatureId)) ClerkBooks();

            if (hours > 0)
            {
                EarnedPerHour = (double)(GM.Contracts.PaidLastTick + GM.Street.LastPennies) / hours;
                NetPerHour = EarnedPerHour - WagesPerHour - CoalBurnPerHour;
            }
        }

        // Pennies an hour coming in from engagements and the street as of the last tick, and
        // the same less wages and coal. Lump payments at an engagement's end aren't counted:
        // this is the steady rate the player can plan purchases against.
        public double EarnedPerHour { get; private set; }
        public double NetPerHour    { get; private set; }

        // Spare watts the street bought on the last tick.
        public static double StreetWatts => GM != null ? GM.Street.LastWatts : 0;

        private void PayWages(double hours)
        {
            var ledger = GM.Ledger;
            if (ApprenticesUnpaid)
            {
                // Back to work once there's an hour's pay in the purse, so they don't
                // stop and start every second on a purse hovering at zero.
                double hourly = Fleet.Sum(g => g.Definition.wagePenniesPerHour);
                if (hourly <= 0 || ledger.Pennies >= (decimal)hourly)
                {
                    ApprenticesUnpaid = false;
                    Restart(g => g.Definition.wagePenniesPerHour > 0);
                }
                return;
            }

            decimal due = (decimal)(WagesPerHour * hours);
            if (due <= 0) return;
            if (ledger.SpendPennies(due)) { _toldUnpaid = false; return; }

            ApprenticesUnpaid = true;
            foreach (var g in Fleet)
                if (g.IsOnline && g.Definition.wagePenniesPerHour > 0) g.Trip("UNPAID");
            if (_toldUnpaid) return;
            _toldUnpaid = true;
            EventBus.Publish(new WorksNoticeEvent("The purse is empty. Your apprentices have downed tools."));
        }

        private void BurnCoal(double hours)
        {
            double burn = CoalBurnPerHour;
            if (burn > 0 && !Hopper.Burn(burn, hours))
            {
                foreach (var g in Fleet)
                    if (g.IsOnline && g.Definition.coalPenniesPerHour > 0) g.Trip("OUT OF COAL");
                EventBus.Publish(new WorksNoticeEvent("The coal hopper is empty. The steam engines have stopped."));
            }

            if (Unlocked(Data.Era2Data.CoalMerchantFeatureId) && HasCoalEngines &&
                Hopper.Fraction(CoalBurnCapacityBasis) < Economy.Era2CoalHopper.LowFraction)
                OrderCoal();
        }

        // The clerk only takes work the works can fully supply on top of what it already owes.
        private void ClerkBooks()
        {
            foreach (var def in Era2Contracts.OrderByDescending(Pay))
            {
                if (!HasFreeSlot) return;
                if (!IsOffered(def)) continue;
                if (CapacityWatts - CommittedWatts >= def.requiredWatts) Book(def);
            }
        }

        private void Restart(Func<Simulation.GeneratorInstance, bool> which)
        {
            foreach (var g in Fleet)
                if (!g.IsOnline && which(g)) g.BringOnline();
        }

        // ---- Save support ----

        public void LoadFromSave(long startDay, double coalLevel, IEnumerable<(string id, long day)> bookedDays)
        {
            StartDay = startDay;
            Hopper.SetFromSave(coalLevel);
            Board.LoadFromSave(bookedDays);
            ApprenticesUnpaid = false;
            _toldUnpaid = false;
            EarnedPerHour = NetPerHour = 0;
        }
    }
}
