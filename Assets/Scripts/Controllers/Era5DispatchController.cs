using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Controllers
{
    // Era 5, the Interconnected Grid: Central Dispatch and the Midlands mimic board. Each
    // city draws reactive power with its real power; capacitor banks supply some of it on the
    // spot. What they don't is carried down the lines from Central — more current, more loss,
    // less of the units' rating left for real power, and a sagging voltage at the far end.
    public class Era5DispatchController : UnitGridController
    {
        public override int EraNumber => Era5Data.EraNumber;
        protected override int BaseSlots => Era5Data.BaseContractSlots;
        protected override DateTime StartDate => new(Era5Data.StartYear, Era5Data.StartMonth, Era5Data.StartDay);
        protected override float StartHour => Era5Data.StartHour;
        protected override bool UnderFrequencyRelays => true;
        // A bank of diesel sets at Central, 200 MW in all (bible §10.3.2: 5–40 MW a set): enough
        // to pick up a city's first substation, inrush and all.
        public override double DieselShare => 0.2;

        private readonly Dictionary<string, Era5Data.CitySpec> _specs = new();
        private readonly Dictionary<string, int> _banks = new();
        private readonly Dictionary<string, double> _voltage = new();
        private readonly Dictionary<string, double> _tripHours = new();

        protected override void CreateAreas()
        {
            foreach (var s in Era5Data.Cities)
            {
                _specs[s.Id] = s;
                _areas.Add(new LoadArea(s.Id, s.BaseGigawatts * 1e9, s.Length));
            }
        }

        protected override void ConnectStartingAreas()
        {
            foreach (var s in Era5Data.Cities)
                if (s.StartsOn) Area(s.Id).Connected = true;
        }

        public Era5Data.CitySpec Spec(string id) => _specs[id];
        public override string AreaName(string areaId) => _specs.TryGetValue(areaId, out var s) ? s.Name : areaId;
        // The mills, potteries and steelworks: the cities with the poorest power factor.
        public override bool IsIndustrial(string areaId) => _specs.TryGetValue(areaId, out var s) && s.PowerFactor < 0.89;

        private static Managers.UpgradeEffectApplier Effects => Managers.UpgradeEffectApplier.Instance;

        // ---- Reactive power ----

        public int Banks(string id) => _banks.TryGetValue(id, out var n) ? n : 0;

        private static double BankStrength => Era5Data.BankShare * (1 + (Effects != null ? Effects.CapacitorStrengthBonus : 0));

        // Reactive watts per real watt still to come down the line from Central (net of banks).
        public double NetReactivePerWatt(string id, int? banks = null)
        {
            var s = _specs[id];
            var a = Area(id);
            double load = Math.Max(a.DemandWatts, a.BaseWatts * 0.5);
            double fromBanks = (banks ?? Banks(id)) * BankStrength * a.BaseWatts;
            return Math.Max(0, (s.ReactivePerWatt * load - fromBanks) / load);
        }

        public double PowerFactor(string id) => 1 / Math.Sqrt(1 + Math.Pow(NetReactivePerWatt(id), 2));

        private static double LineFactor => Effects != null ? Effects.LineLossMultiplier : 1.0;

        // Per-unit voltage at the city: 1 less the line's drop (P·R + Q·X), plus the tap changers.
        public double VoltageAt(string id, int? banks = null)
        {
            var s = _specs[id];
            var a = Area(id);
            double loading = a.BaseWatts > 0 ? Math.Max(a.DemandWatts, a.BaseWatts * 0.6) / a.BaseWatts : 1;
            double q = NetReactivePerWatt(id, banks);
            double drop = loading * s.Length * LineFactor *
                          (Era5Data.ResistancePerLength + Era5Data.ReactancePerLength * q);
            return 1 - drop + (Effects != null ? Effects.VoltageSupport : 0);
        }

        public double Voltage(string id) => _voltage.TryGetValue(id, out var v) ? v : 1;

        protected override void UpdateLines()
        {
            foreach (var a in _areas)
            {
                var s = _specs[a.Id];
                a.LossPerWatt = Era5Data.ResistancePerLength * s.Length * LineFactor / Math.Max(1, a.BaseWatts);
                a.ReactivePerWatt = NetReactivePerWatt(a.Id);
                _voltage[a.Id] = VoltageAt(a.Id);
            }
        }

        // A city with a sagging voltage pays less: dim lamps, slow motors (bible §4.5).
        protected override double PayFactor(LoadArea area)
        {
            double v = Voltage(area.Id);
            if (v >= Era5Data.GoodVoltage) return 1;
            if (v >= Era5Data.PoorVoltage) return 1 - (Era5Data.GoodVoltage - v) * 4;
            return 0.6;
        }

        // The units make the region's reactive power too, out of their rating.
        protected override double Dispatch(double wantedWatts, double dt, double gameHours)
        {
            double q = 0, capacity = 0;
            foreach (var a in _areas) if (a.Supplied) q += a.DemandWatts * a.ReactivePerWatt;
            foreach (var u in _units) if (u.IsOnline) capacity += u.Capacity(SimulationTick.Instance?.Grid);
            double share = capacity > 0 ? Math.Min(0.8, q / capacity) : 0;
            Grid.CapacityFactor = Math.Sqrt(1 - share * share);
            return base.Dispatch(wantedWatts, dt, gameHours);
        }

        // Real over apparent power at Central (the mockup's PF reading).
        public double SystemPowerFactor
        {
            get
            {
                double p = 0, q = 0;
                foreach (var a in _areas) if (a.Supplied) { p += a.DeliveredWatts; q += a.DeliveredWatts * a.ReactivePerWatt; }
                return p > 0 ? p / Math.Sqrt(p * p + q * q) : 1;
            }
        }

        // The lowest voltage on the board, in kV (the mockup's V reading).
        public double WorstKilovolts =>
            _areas.Where(a => a.Supplied).Select(a => Voltage(a.Id)).DefaultIfEmpty(1).Min() * Era5Data.NominalKilovolts;

        public LoadArea WorstCity =>
            _areas.Where(a => a.Connected).OrderBy(a => Voltage(a.Id)).FirstOrDefault();

        public decimal BankCost(string id) => Managers.Money.FromPounds(_specs[id].BankPounds * (1 + 0.25 * Banks(id)));

        public bool CanAddBank(string id) =>
            IsCurrentEra && Area(id).Connected && Banks(id) < Era5Data.MaxBanks && GM.Ledger.Pennies >= BankCost(id);

        public bool AddBank(string id)
        {
            if (!CanAddBank(id) || !GM.Ledger.SpendPennies(BankCost(id))) return false;
            _banks[id] = Banks(id) + 1;
            return true;
        }

        protected override void AfterShare(double gameHours)
        {
            base.AfterShare(gameHours);
            if (GM.Contracts.ShortfallPaused || BlackStart != null) return;
            // Voltage collapse: a city whose voltage falls too far trips its own load off, and
            // comes back after a while if things are better (bible §4.5).
            foreach (var a in _areas.Where(a => a.Connected))
            {
                if (_tripHours.TryGetValue(a.Id, out var h))
                {
                    h -= gameHours;
                    if (h <= 0 && VoltageAt(a.Id) >= Era5Data.PoorVoltage) { _tripHours.Remove(a.Id); a.Shed = false; }
                    else _tripHours[a.Id] = Math.Max(0.05, h);
                }
                else if (!a.Shed && Voltage(a.Id) < Era5Data.CollapseVoltage)
                {
                    a.Shed = true;
                    _tripHours[a.Id] = Era5Data.ReconnectAfterHours;
                    EventBus.Publish(new WorksNoticeEvent(
                        $"{AreaName(a.Id)}'s voltage collapsed and its load tripped. Capacitor banks would hold it up."));
                }
            }
        }

        public bool VoltageTripped(string id) => _tripHours.ContainsKey(id);

        // The first brownout introduces the redundancy score (bible §14.2.3).
        protected override void OnBrownout() =>
            Introduce(Era5Data.RedundancyScore, "Redundancy",
                      "The relays shed load and kept the grid up, but bringing it back showed how few units could start " +
                      "a dead grid on their own. Fit more units for black start.");

        // ---- Black-start fitting (redundancy) ----

        public bool CanFitBlackStart => IsUnlockedFeature(Era5Data.BlackStartId) || IsIntroduced(Era5Data.RedundancyScore);

        public GridUnit NextToFit =>
            CanFitBlackStart ? _units.Where(u => !u.Inherited && !u.BlackStartCapable && u.State != UnitState.Faulted)
                                     .OrderBy(u => u.Definition.baseCostPennies).FirstOrDefault()
                             : null;

        public decimal FitCost(GridUnit u) => (decimal)(u.Definition.baseCostPennies * Era5Data.BlackStartFitShare);

        public bool FitBlackStart()
        {
            var u = NextToFit;
            if (u == null || !GM.Ledger.SpendPennies(FitCost(u))) return false;
            u.BlackStartCapable = true;
            return true;
        }

        // Bible §11.1: count and diversity of black-start units (the old diesels count once).
        public double RedundancyScore
        {
            get
            {
                var capable = _units.Where(u => !u.Inherited && u.BlackStartCapable && u.State != UnitState.Faulted).ToList();
                int kinds = capable.Select(u => u.Definition.generatorType).Distinct().Count();
                return Math.Min(100, 20 + 20 * capable.Count + 10 * Math.Max(0, kinds - 1));
            }
        }

        // ---- Cities ----

        public decimal ConnectCost(string id) => Managers.Money.FromPounds(_specs[id].ConnectPounds);

        public bool CanConnect(string id)
        {
            if (!IsCurrentEra || BlackStart != null || Area(id).Connected) return false;
            string parent = _specs[id].Parent;
            if (parent != null && !Area(parent).Connected) return false;
            return GM.Ledger.Pennies >= ConnectCost(id);
        }

        public bool Connect(string id)
        {
            if (!CanConnect(id) || !GM.Ledger.SpendPennies(ConnectCost(id))) return false;
            Area(id).Connected = true;
            EventBus.Publish(new WorksNoticeEvent($"{_specs[id].Name} is on the grid."));
            return true;
        }

        // ---- Save ----

        protected override void WriteArea(LoadArea area, Save.SaveData.LoadAreaEntry entry) => entry.capacitorBanks = Banks(area.Id);

        protected override void ReadArea(LoadArea area, Save.SaveData.LoadAreaEntry entry)
        {
            if (entry.capacitorBanks > 0) _banks[area.Id] = Math.Min(entry.capacitorBanks, Era5Data.MaxBanks);
            // A city tripped on voltage is back on at load: the trip is only a moment's state.
            if (entry.shed && !entry.connected) area.Shed = false;
        }

        protected override void ResetAreaExtras()
        {
            _banks.Clear();
            _tripHours.Clear();
            _voltage.Clear();
        }
    }
}
