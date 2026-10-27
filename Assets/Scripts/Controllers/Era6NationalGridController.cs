using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitMode = IdlePowerGen.Simulation.GridUnit.UnitMode;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Controllers
{
    // Era 6, the Nuclear Age: the National Grid's control room. Big machines are slow
    // (bible §2.2). Each unit runs on BASE (flat out) or AGC (following the load, cheapest
    // first, within its ramp rate), or waits on standby, and takes time to start from there.
    // Reactors only run on BASE, so the night trough has to stay above what they send; the
    // evening rise needs units started ahead of it. Operator training and drills arrive here.
    public class Era6NationalGridController : UnitGridController
    {
        public override int EraNumber => Era6Data.EraNumber;
        protected override int BaseSlots => Era6Data.BaseContractSlots;
        protected override DateTime StartDate => new(Era6Data.StartYear, Era6Data.StartMonth, Era6Data.StartDay);
        protected override float StartHour => Era6Data.StartHour;
        protected override bool UnderFrequencyRelays => true;
        // The old fleet's black-start diesels and gas turbines, 10 GW in all: enough to pick up
        // the Midlands' first substation, inrush and all.
        public override double DieselShare => 0.2;
        protected override double StartCoalTonnes => Era6Data.StartCoalTonnes;
        protected override double CoalPoundsPerTonne => Era6Data.CoalPoundsPerTonne;
        // Trained operators see it coming sooner (bible §11.2: OTP and the warning time).
        protected override double CascadeSeconds =>
            Era4Data.CascadeWarningSeconds + Era6Data.CascadeBonusSeconds * TrainingScore / 100;
        // Auto-synchronisers are standard by 1978: a unit that has run up closes on its own.
        public override bool AutoSync => true;

        private readonly Dictionary<string, Era6Data.RegionSpec> _specs = new();
        private static Managers.UpgradeEffectApplier Effects => Managers.UpgradeEffectApplier.Instance;

        protected override void Awake()
        {
            base.Awake();
            Grid.MeritOrder = true;
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        // ---- Regions ----

        protected override void CreateAreas()
        {
            foreach (var s in Era6Data.Regions)
            {
                _specs[s.Id] = s;
                _areas.Add(new LoadArea(s.Id, s.BaseGigawatts * 1e9, s.Length));
            }
        }

        // Already carrying its load when the era opens: no ramp-in for the fleet to chase.
        protected override void ConnectStartingAreas()
        {
            foreach (var s in Era6Data.Regions)
                if (s.StartsOn) Area(s.Id).SetFromSave(true, false);
        }

        public Era6Data.RegionSpec Spec(string id) => _specs[id];
        public override string AreaName(string areaId) => _specs.TryGetValue(areaId, out var s) ? s.Name : areaId;
        public override bool IsIndustrial(string areaId) => _specs.TryGetValue(areaId, out var s) && s.Industrial;

        // The supergrid's lines lose a little, more the further the region is from the Midlands.
        protected override void UpdateLines()
        {
            foreach (var a in _areas)
                a.LossPerWatt = Era6Data.LossPerLength * _specs[a.Id].Length / Math.Max(1, a.BaseWatts);
        }

        // Economy 7 moves part of the daily swing into the night (the shape still averages 1).
        public double Flattening => Effects != null ? Math.Min(0.8, Effects.DemandFlattening) : 0;

        protected override double AreaShape(LoadArea area, double hourOfDay)
        {
            double shaped = base.AreaShape(area, hourOfDay);
            double level = 1 - ShedFractions[SheddingStage];
            return shaped + Flattening * (level - shaped);
        }

        public override decimal ConnectCost(string id) => Managers.Money.FromPounds(_specs[id].ConnectPounds);

        public override bool CanConnect(string id)
        {
            if (!IsCurrentEra || BlackStart != null || Area(id).Connected) return false;
            string parent = _specs[id].Parent;
            if (parent != null && !Area(parent).Connected) return false;
            return GM.Ledger.Pennies >= ConnectCost(id);
        }

        public override bool Connect(string id)
        {
            if (!CanConnect(id) || !GM.Ledger.SpendPennies(ConnectCost(id))) return false;
            Area(id).Connected = true;
            EventBus.Publish(new WorksNoticeEvent($"{_specs[id].Name} is on the National Grid."));
            return true;
        }

        // ---- Units: modes, standby and starting ----

        public bool AgcAvailable => IsUnlockedFeature(Era6Data.AgcId);

        // A new unit runs up from cold; it follows the load if it can and AGC is there to steer it.
        protected override void OnUnitBuilt(GridUnit unit)
        {
            unit.Mode = unit.CanFollow && AgcAvailable ? UnitMode.Agc : UnitMode.Base;
            unit.SetState(UnitState.Starting, StartHours(unit));
            EventBus.Publish(new WorksNoticeEvent($"{BoardName(unit)} is built and starting: on the bars in {Hours(StartHours(unit))}."));
        }

        protected override void ReturnToService(GridUnit unit)
        {
            if (unit.ResumeOnline) unit.SetState(UnitState.Starting, StartHours(unit));
            else unit.SetState(UnitState.Standby);
        }

        // A reactor's service is its refuelling outage.
        protected override double ServiceHoursFor(GridUnit unit) =>
            Era4Data.ServiceHours * (unit.CanFollow ? 1 : Era6Data.ReactorServiceFactor);

        public static double StartHours(GridUnit unit) => unit.Inherited ? 0 : unit.Definition.startHours;

        public bool CanSetMode(GridUnit u, UnitMode mode) =>
            IsCurrentEra && BlackStart == null && u != null && !u.Inherited && u.Mode != mode &&
            (mode == UnitMode.Base || u.CanFollow && AgcAvailable);

        public bool SetMode(GridUnit u, UnitMode mode)
        {
            if (!CanSetMode(u, mode)) return false;
            u.Mode = mode;
            return true;
        }

        public bool CanStandDown(GridUnit u) =>
            IsCurrentEra && BlackStart == null && u != null && !u.Inherited &&
            (u.State == UnitState.Online || u.State == UnitState.AwaitingSync || u.State == UnitState.Starting);

        // Off the bars and cold. A reactor stood down by plan can start again whenever.
        public bool StandDown(GridUnit u)
        {
            if (!CanStandDown(u)) return false;
            u.SetState(UnitState.Standby);
            return true;
        }

        public bool CanStart(GridUnit u) =>
            IsCurrentEra && BlackStart == null && u != null && u.State == UnitState.Standby && u.HoursLeft <= 0;

        // The cascade warning's remedy in Era 6: the quickest unit on standby.
        public GridUnit FastestStandby =>
            _units.Where(CanStart).OrderBy(StartHours).ThenBy(RunCost).FirstOrDefault();

        public bool StartUnit(GridUnit u)
        {
            if (!CanStart(u)) return false;
            u.SetState(UnitState.Starting, StartHours(u));
            return true;
        }

        // A dark grid scrams its reactors: they hold until the xenon clears. Starts in hand stop.
        protected override void AfterBlackout()
        {
            foreach (var u in _units.Where(u => !u.Inherited))
            {
                if (!u.CanFollow && u.State == UnitState.AwaitingSync) u.SetState(UnitState.Standby, Era6Data.ReactorHoldHours);
                else if (u.State == UnitState.Starting) u.SetState(UnitState.Standby);
            }
        }

        // ---- The board ----

        public override string BoardName(GridUnit u)
        {
            if (u.Inherited) return "OLD FLEET";
            var type = u.Definition.generatorType;
            int i = 0;
            foreach (var x in _units)
                if (!x.Inherited && x.Definition.generatorType == type && x.Number < u.Number) i++;
            var sites = Era6Data.Sites(type);
            string site = sites[i % sites.Length] + (i >= sites.Length ? $" {i / sites.Length + 1}" : "");
            return $"{Era6Data.Prefix(type)}-{i + 1:00} {site}";
        }

        // Pounds per MWh to run it, fuel included: the merit order.
        public double RunCost(GridUnit u)
        {
            if (u.Inherited) return Era6Data.OldFleetMeritPounds;
            double price = Effects != null ? Effects.FuelPriceMultiplier : 1.0;
            double burn = Effects != null ? Effects.FuelBurnMultiplier : 1.0;
            return (u.Definition.fuelPoundsPerMWh + u.Definition.coalTonnesPerMWh * CoalPoundsPerTonne * burn) * price;
        }

        // ---- Reserve (bible Appendix A, Era 6: "RESERVE LOW 340 < 500 MW") ----

        // The biggest single unit on the bars: what the reserve must cover if it trips.
        public double MarginWatts =>
            _units.Where(u => !u.Inherited && u.IsOnline).Select(u => u.OutputWatts).DefaultIfEmpty(0).Max();

        // Standby units that could be on the bars within minutes.
        public double FastStandbyWatts =>
            _units.Where(u => !u.Inherited && u.State == UnitState.Standby && u.HoursLeft <= 0 &&
                              StartHours(u) <= Era6Data.FastStartHours)
                  .Sum(u => u.Capacity(SimulationTick.Instance?.Grid));

        // Spinning reserve plus the fast units on standby.
        public double TotalReserveWatts => ReserveWatts + FastStandbyWatts;
        public bool ReserveLow => MarginWatts > 0 && TotalReserveWatts < MarginWatts;

        // What units on BASE and the others' minimums must send, whatever the load: those on
        // the bars and those starting, which soon will be.
        public double FloorWatts
        {
            get
            {
                var grid = SimulationTick.Instance?.Grid;
                double floor = 0;
                foreach (var u in _units.Where(u => !u.Inherited && (u.IsOnline || u.State == UnitState.Starting ||
                                                                     u.State == UnitState.AwaitingSync)))
                    floor += u.Capacity(grid) * (u.Following ? u.MinStableShare : 1);
                return floor * Grid.CapacityFactor;
            }
        }

        // The lowest demand over the next game hours, and how many hours away it is.
        public (double Watts, double HoursAhead) TroughAhead(double hours = 6)
        {
            double now = Now / 3600.0;
            double low = double.MaxValue, at = 0;
            for (double h = 0; h <= hours; h += 0.25)
            {
                double w = DemandAt((now + h) % 24.0);
                if (w < low) { low = w; at = h; }
            }
            return (low, at);
        }

        // ---- The tick ----

        public double FuelPoundsPerHour { get; private set; }
        private double _overSeconds;

        protected override double Dispatch(double wantedWatts, double dt, double gameHours)
        {
            foreach (var u in _units) u.MeritCost = RunCost(u);
            // A drill's contracts wait it out (it was announced); set each tick, so never left on.
            GM.Contracts.Held = BlackStart != null && BlackStart.Drill;
            _dt = dt;
            return base.Dispatch(wantedWatts, dt, gameHours);
        }

        private double _dt = 1;

        protected override void AfterShare(double gameHours)
        {
            base.AfterShare(gameHours);

            // Gas, oil and uranium are bought as they burn (coal comes from the yard).
            double pounds = 0;
            double price = Effects != null ? Effects.FuelPriceMultiplier : 1.0;
            foreach (var u in _units.Where(u => !u.Inherited && u.IsOnline))
                pounds += u.OutputWatts / 1e6 * u.Definition.fuelPoundsPerMWh * price;
            FuelPoundsPerHour = pounds;
            if (pounds > 0) GM.Ledger.DrainPennies(Managers.Money.FromPounds(pounds * gameHours));

            if (GM.Contracts.ShortfallPaused || BlackStart != null) { _overSeconds = 0; return; }
            Overspeed();
            if (IsUnlockedFeature(Era6Data.CommitmentId)) Commit();
            if (IsUnlockedFeature(Era6Data.OutagesId)) PlanOutages();
        }

        // Held over 51 Hz, overspeed protection trips the biggest unit on BASE.
        private void Overspeed()
        {
            if (Frequency < Era6Data.TripHz) { _overSeconds = 0; return; }
            _overSeconds += _dt;
            if (_overSeconds < Era6Data.TripSeconds) return;
            _overSeconds = 0;
            var unit = _units.Where(u => !u.Inherited && u.IsOnline).OrderBy(u => u.Following ? 1 : 0)
                             .ThenByDescending(u => u.OutputWatts).FirstOrDefault();
            if (unit == null) return;
            bool reactor = !unit.CanFollow;
            unit.SetState(UnitState.Standby, reactor ? Era6Data.ReactorHoldHours : Era6Data.TripHoldHours);
            EventBus.Publish(new WorksNoticeEvent(
                $"Too much on the bars: overspeed protection tripped {BoardName(unit)}." +
                (reactor ? $" The reactor can't restart for {Era6Data.ReactorHoldHours:0} hours." : "")));
        }

        // The unit commitment computer: starts standby units in time for the peak, cheapest to
        // run first, and stands the dearest down when the night would leave too much on the bars.
        private void Commit()
        {
            double margin = MarginWatts;
            var grid = SimulationTick.Instance?.Grid;
            foreach (var u in _units.Where(u => CanStart(u)).OrderBy(RunCost))
            {
                var (peak, _) = PeakAhead(StartHours(u) + 0.5);
                if (peak + margin <= ReadyCapacity) break;
                StartUnit(u);
                return;
            }
            var (low, _) = TroughAhead(1);
            if (FloorWatts <= low) return;
            foreach (var u in _units.Where(u => !u.Inherited && u.IsOnline && u.Following && u.MinStableShare > 0)
                                    .OrderByDescending(RunCost))
            {
                var (peak, _) = PeakAhead(StartHours(u) + 0.5);
                if (ReadyCapacity - u.Capacity(grid) * Grid.CapacityFactor < peak + margin) continue;
                StandDown(u);
                return;
            }
        }

        // Outage planning: one unit at a time into service before it runs overdue, when the
        // rest can cover it (standby units first: they cost nothing to take off).
        private void PlanOutages()
        {
            if (_units.Any(u => u.State == UnitState.InService)) return;
            double margin = MarginWatts;
            var due = _units.Where(u => !u.Inherited && u.HoursSinceService > u.Definition.serviceIntervalHours * 0.85 &&
                                        (u.State == UnitState.Standby || u.IsOnline))
                            .OrderBy(u => u.State == UnitState.Standby ? 0 : 1).ThenByDescending(u => u.HoursSinceService);
            foreach (var u in due)
            {
                bool covered = u.State == UnitState.Standby ||
                               Grid.OnlineCapacity - u.Capacity(SimulationTick.Instance?.Grid) * Grid.CapacityFactor - Grid.Load >= margin;
                if (covered && Service(u)) return;
            }
        }

        // ---- Operator training and drills (bible §10.4, §11.1, §14.2.5) ----

        private double _drillScore, _drillGameTime;

        private void OnUpgradePurchased(UpgradePurchasedEvent e)
        {
            if (e.Definition.upgradeId != Era6Data.TrainingId || !IsCurrentEra) return;
            Introduce(Era6Data.TrainingScore, "Operator training",
                      "The control room has written procedures for every emergency, and the operators have been through " +
                      "them. Drill them on a black start (customers warned, nothing lost) and they'll be quicker when it's real.");
        }

        public override double TrainingScore
        {
            get
            {
                if (!IsIntroduced(Era6Data.TrainingScore)) return 0;
                double days = (Now - _drillGameTime) / 86400.0;
                double drill = _drillScore * Math.Clamp(1 - days / Era6Data.DrillFreshDays, 0, 1);
                return Math.Min(100, Era6Data.TrainingBase + (Effects != null ? Effects.TrainingBonus : 0) + drill);
            }
        }

        public decimal DrillCost => Managers.Money.FromPounds(Era6Data.DrillPounds);

        public bool CanDrill =>
            IsCurrentEra && IsIntroduced(Era6Data.TrainingScore) && BlackStart == null && CascadeSecondsLeft < 0 &&
            !Grid.Dead && GM.Ledger.Pennies >= DrillCost;

        // The grid taken down on purpose to practise the black start.
        public bool StartDrill()
        {
            if (!CanDrill || !GM.Ledger.SpendPennies(DrillCost)) return false;
            GM.Contracts.Held = true;
            Blackout(drill: true);
            return true;
        }

        protected internal override void AfterBlackStart(BlackStartSession session)
        {
            if (!session.Drill) return;
            _drillScore = Era6Data.DrillPoints * session.DrillPerformance;
            _drillGameTime = Now;
            GM.Contracts.Held = false;
            EventBus.Publish(new WorksNoticeEvent($"Drill complete: operator training {TrainingScore:0}."));
        }

        // ---- The status strip ----

        public override (int Level, string Message)? EraAdvice()
        {
            if (BlackStart != null || Grid.Dead) return null;
            double f = Frequency;
            if (f > Era6Data.HighHz)
                return (3, $"{f:0.00} Hz: more on the bars than the load wants. Stand a unit down or put one on AGC. " +
                           $"Over {Era6Data.TripHz:0} Hz, protection trips the biggest unit.");

            var (low, lowAhead) = TroughAhead();
            double floor = FloorWatts;
            if (floor > low)
                return (2, $"{At(lowAhead)} demand falls to {NumberFormatter.FormatWatts(low)}, under the " +
                           $"{NumberFormatter.FormatWatts(floor)} that BASE and minimum output must send. Stand a unit down before then.");

            var (peak, peakAhead) = PeakAhead();
            double online = Grid.OnlineCapacity;
            if (peak > online)
            {
                double ready = ReadyCapacity;
                if (ready >= peak)
                    return (1, $"Units starting will cover the peak of {NumberFormatter.FormatWatts(peak)} {At(peakAhead).ToLowerInvariant()}.");
                var start = _units.Where(u => CanStart(u) && StartHours(u) <= peakAhead).OrderBy(RunCost).FirstOrDefault();
                return start != null
                    ? (2, $"{At(peakAhead)} demand reaches {NumberFormatter.FormatWatts(peak)}, {NumberFormatter.FormatWatts(peak - ready)} " +
                          $"more than is running or starting. Start {BoardName(start)} now: it takes {Hours(StartHours(start))}.")
                    : (3, $"{At(peakAhead)} demand reaches {NumberFormatter.FormatWatts(peak)}, {NumberFormatter.FormatWatts(peak - ready)} " +
                          "more than is running, and nothing on standby can start in time. Build a fast unit or drop a region.");
            }
            if (ReserveLow)
                return (2, $"Reserve {NumberFormatter.FormatWatts(TotalReserveWatts)} is less than the biggest unit on the bars " +
                           $"({NumberFormatter.FormatWatts(MarginWatts)}). If it trips, the relays will shed load.");
            return null;
        }

        // A time ahead as the clock will read it.
        private static string At(double hoursAhead)
        {
            if (hoursAhead < 0.25) return "Now";
            double at = (Now / 3600.0 + hoursAhead) % 24.0;
            int h = (int)at, m = (int)((at - h) * 60) / 15 * 15;
            return $"At {h:00}:{m:00}";
        }

        public static string Hours(double hours) =>
            hours < 1 ? $"{Math.Max(1, Math.Round(hours * 60)):0} min"
                      : hours % 1 == 0 ? $"{hours:0} h" : $"{Math.Floor(hours):0} h {Math.Round(hours % 1 * 60):0} min";

        // ---- Era start and save ----

        protected override void OnEraStart()
        {
            base.OnEraStart();
            _drillScore = _drillGameTime = 0;
            _overSeconds = 0;
        }

        public override void WriteSave(Save.SaveData data)
        {
            base.WriteSave(data);
            if (!IsCurrentEra) return;
            data.drillScore = _drillScore;
            data.drillGameTime = _drillGameTime;
        }

        public override void ReadSave(Save.SaveData data)
        {
            base.ReadSave(data);
            _overSeconds = 0;
            bool here = (int)data.currentEra == EraNumber;
            _drillScore = here ? data.drillScore : 0;
            _drillGameTime = here ? data.drillGameTime : 0;
        }
    }
}
