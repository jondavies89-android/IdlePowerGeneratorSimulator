using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Models;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Controllers
{
    // What Eras 4 and 5 share: a grid of units brought on one at a time and synchronised by
    // hand, its frequency (GridDispatch), maintenance and coal, the preparedness scores they
    // introduce, the cascade warning and the blackout that ends it in a black start.
    // Failures only happen while the player is here: never during offline catch-up (CLAUDE.md,
    // bible §10.5).
    public abstract class UnitGridController : GridWorksController
    {
        public IReadOnlyList<GridUnit> Units => _units;
        protected readonly List<GridUnit> _units = new();

        public GridDispatch Grid { get; private set; }
        public double Frequency => Grid.Frequency;

        protected SeededRandom Faults { get; private set; }
        protected SeededRandom SyncRandom { get; private set; }

        // ---- Era constants (Era 4's unless an era says otherwise) ----
        protected virtual double AmberHz => Data.Era4Data.AmberHz;
        protected virtual double WarnHz => Data.Era4Data.WarnHz;
        protected virtual double ClearHz => Data.Era4Data.ClearHz;
        protected virtual double CollapseHz => Data.Era4Data.CollapseHz;
        protected virtual double CascadeSeconds => Data.Era4Data.CascadeWarningSeconds;
        // Era 5's automatic under-frequency relays (bible §4.4).
        protected virtual bool UnderFrequencyRelays => false;
        // The old station's black-start diesel set, as a share of the inherited output: 5 MW in
        // Era 4, within the bible's 5–40 MW (§10.3.2).
        public virtual double DieselShare => 0.5;
        protected virtual double StartCoalTonnes => Data.Era4Data.StartCoalTonnes;
        protected virtual double CoalPoundsPerTonne => Data.Era4Data.CoalPoundsPerTonne;

        // ---- Era 6's hooks: units that start from standby, and trained operators ----

        // A unit just bought: Eras 4–5 run it up at once, waiting to be synchronised.
        protected virtual void OnUnitBuilt(GridUnit unit) { }

        // A unit back from a service or a repair, or with coal in the yard again.
        protected virtual void ReturnToService(GridUnit unit) => unit.SetState(UnitState.AwaitingSync);

        // Game hours a service takes this unit.
        protected virtual double ServiceHoursFor(GridUnit unit) => Data.Era4Data.ServiceHours;

        // Operator training (bible §11.1 OTP), 0–100; it only ever helps (Era 6 on).
        public virtual double TrainingScore => 0;

        // The board's name for a unit ("NUC-04 HARTLEPOOL"); null for the plain "UNIT 3 COAL".
        public virtual string BoardName(GridUnit unit) => null;

        // The era's own line for the status strip, worst first (0 healthy … 3 alarm), or null.
        public virtual (int Level, string Message)? EraAdvice() => null;

        // ---- Connecting load areas (Eras 4–6 each price their own) ----

        public abstract decimal ConnectCost(string id);
        public abstract bool CanConnect(string id);
        public abstract bool Connect(string id);

        protected override void Awake()
        {
            base.Awake();
            Grid = new GridDispatch(new SeededRandom(seed: 4001 + EraNumber));
            Faults = new SeededRandom(seed: 4101 + EraNumber);
            SyncRandom = new SeededRandom(seed: 4201 + EraNumber);
            EventBus.Subscribe<EraAdvancedEvent>(OnEraLeft);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraLeft);
        }

        // Leaving the era: its units become the next era's inherited station.
        private void OnEraLeft(EraAdvancedEvent e)
        {
            if (e.FromEra == EraNumber && e.ToEra != EraNumber) ClearUnits();
        }

        private static PowerGrid PowerGrid => SimulationTick.Instance?.Grid;
        private static Managers.UpgradeEffectApplier Effects => Managers.UpgradeEffectApplier.Instance;

        public bool IsUnlockedFeature(string id) => Effects != null && Effects.IsFeatureUnlocked(id);

        // ---- Era start and save ----

        protected override void OnEraStart()
        {
            ClearUnits();
            Grid.Reset();
            CoalTonnes = StartCoalTonnes;
            CoalTargetDays = 0;
            BlackStart = null;
            CascadeSecondsLeft = -1;
            SheddingStage = 0;
            ConnectStartingAreas();
        }

        protected abstract void ConnectStartingAreas();

        private void ClearUnits()
        {
            foreach (var u in _units)
                if (!u.Inherited) PowerGrid?.RemoveGenerator(u.Instance);
            _units.Clear();
            _units.Add(new GridUnit(Managers.EraManager.InheritedWattsFor(EraNumber)));
        }

        public GridUnit InheritedUnit => _units.FirstOrDefault(u => u.Inherited);

        public override void WriteSave(Save.SaveData data)
        {
            base.WriteSave(data);
            if (!IsCurrentEra) return;
            data.coalStockTonnes = CoalTonnes;
            data.coalTargetDays = CoalTargetDays;
            data.introducedScores = new List<string>(Introduced);
            foreach (var u in _units.Where(u => !u.Inherited))
                data.units.Add(new Save.SaveData.UnitEntry
                {
                    generatorType = u.Definition.generatorType.ToString(), number = u.Number,
                    // A unit mid-sync or in a dead grid comes back waiting to be synchronised.
                    state = (u.State == UnitState.Online && BlackStart != null ? UnitState.AwaitingSync : u.State).ToString(),
                    hoursSinceService = u.HoursSinceService, hoursLeft = u.HoursLeft, blackStart = u.BlackStartCapable,
                    mode = u.Mode.ToString(), backToStandby = !u.ResumeOnline,
                });
        }

        public override void ReadSave(Save.SaveData data)
        {
            ClearUnits();
            Grid.Reset();
            BlackStart = null;
            CascadeSecondsLeft = -1;
            SheddingStage = 0;
            CoalTonnes = 0;
            CoalTargetDays = 0;
            LoadIntroduced(data.introducedScores);
            base.ReadSave(data);
            if ((int)data.currentEra != EraNumber) return;

            if (data.units != null && data.units.Count > 0)
            {
                foreach (var e in data.units)
                {
                    if (!Enum.TryParse<GeneratorType>(e.generatorType, out var type)) continue;
                    var def = GM.GeneratorCatalogue.FirstOrDefault(g => g.generatorType == type);
                    if (def == null) continue;
                    var unit = AddUnit(def, e.number);
                    var state = Enum.TryParse<UnitState>(e.state, out var s) ? s : UnitState.AwaitingSync;
                    unit.SetState(state, e.hoursLeft);
                    unit.HoursSinceService = e.hoursSinceService;
                    unit.BlackStartCapable = e.blackStart || def.blackStartCapable;
                    if (Enum.TryParse<GridUnit.UnitMode>(e.mode, out var mode)) unit.Mode = mode;
                    unit.ResumeOnline = !e.backToStandby;
                }
                CoalTonnes = data.coalStockTonnes;
            }
            else CoalTonnes = data.coalStockTonnes > 0 ? data.coalStockTonnes : StartCoalTonnes;
            CoalTargetDays = data.coalTargetDays;
        }

        // ---- Units ----

        private GridUnit AddUnit(GeneratorDefinition def, int number)
        {
            var instance = new GeneratorInstance(def);
            var unit = new GridUnit(instance, number) { BlackStartCapable = def.blackStartCapable };
            PowerGrid?.AddGenerator(instance);
            _units.Add(unit);
            return unit;
        }

        public int Owned(GeneratorType type) => _units.Count(u => !u.Inherited && u.Definition.generatorType == type);

        public IEnumerable<GeneratorDefinition> UnitKinds =>
            GM == null ? Enumerable.Empty<GeneratorDefinition>()
                       : GM.GeneratorCatalogue.Where(g => g.minEra == EraNumber).OrderBy(g => g.baseCostPennies);

        public bool IsUnlocked(GeneratorDefinition def) =>
            string.IsNullOrEmpty(def.requiredUpgradeId) || GM.Upgrades.IsPurchased(def.requiredUpgradeId);

        public decimal NextCost(GeneratorDefinition def) =>
            (decimal)Math.Ceiling(def.baseCostPennies * Math.Pow(Era2GeneratorController.CostGrowth, Owned(def.generatorType)));

        public bool AtLimit(GeneratorDefinition def) => def.maxUnits > 0 && Owned(def.generatorType) >= def.maxUnits;

        public bool CanBuy(GeneratorDefinition def) =>
            IsCurrentEra && IsUnlocked(def) && !AtLimit(def) && BlackStart == null && GM.Ledger.Pennies >= NextCost(def);

        // A new unit is built and run up to speed; it carries nothing until synchronised.
        public GridUnit BuyUnit(GeneratorDefinition def)
        {
            if (!CanBuy(def) || !GM.Ledger.SpendPennies(NextCost(def))) return null;
            int number = _units.Count == 0 ? 1 : _units.Max(u => u.Number) + 1;
            var unit = AddUnit(def, number);
            OnUnitBuilt(unit);
            if (unit.State == UnitState.AwaitingSync)
                EventBus.Publish(new WorksNoticeEvent($"Unit {unit.Number} is built and running up. Synchronise it to carry load."));
            return unit;
        }

        public IEnumerable<GridUnit> AwaitingSync => _units.Where(u => u.State == UnitState.AwaitingSync);
        public GridUnit NextToSync => AwaitingSync.OrderBy(u => u.Number).FirstOrDefault();

        public virtual bool AutoSync => IsUnlockedFeature(Data.Era4Data.AutoSyncFeatureId);

        // Trained operators get a wider clean close (bible §11.2); untrained, the Era 4 window.
        public double SyncForgiveness => 1 + Data.Era6Data.SyncForgiveness * TrainingScore / 100;

        // Opens the synchroscope on a unit waiting for it.
        public Synchroscope BeginSync(GridUnit unit)
        {
            if (unit == null || unit.State != UnitState.AwaitingSync || Grid.Dead) return null;
            return new Synchroscope(Grid.Frequency, SyncRandom) { Forgiveness = SyncForgiveness };
        }

        // The breaker is closed: clean, with a jolt, or slammed out of phase.
        public void EndSync(GridUnit unit, Synchroscope.Close result)
        {
            if (unit == null || unit.State != UnitState.AwaitingSync) return;
            switch (result)
            {
                case Synchroscope.Close.Clean:
                    unit.Synchronise();
                    break;
                case Synchroscope.Close.Rough:
                    unit.Synchronise();
                    Grid.Kick(-0.15);
                    EventBus.Publish(new WorksNoticeEvent($"Unit {unit.Number} is on, with a jolt. Closer to twelve o'clock next time."));
                    break;
                default:
                    unit.SetState(UnitState.Faulted, Data.Era4Data.SlamRepairHours * ServiceTime);
                    Grid.Kick(-0.4);
                    EventBus.Publish(new WorksNoticeEvent($"Unit {unit.Number}'s breaker slammed out of phase. The shaft needs looking at."));
                    break;
            }
        }

        // ---- Maintenance ----

        private static double ServiceTime => Effects != null ? Effects.ServiceTimeMultiplier : 1.0;

        public bool IsOverdue(GridUnit u) =>
            !u.Inherited && u.HoursSinceService > u.Definition.serviceIntervalHours;

        public IEnumerable<GridUnit> OverdueUnits => _units.Where(u => IsOverdue(u) && u.State == UnitState.Online);

        public decimal ServiceCost(GridUnit u) =>
            (decimal)(u.Definition.baseCostPennies * Data.Era4Data.ServiceCostShare);

        // Era 6: a unit on standby can be serviced too (the best time for it).
        public bool CanService(GridUnit u) =>
            !u.Inherited && (u.State == UnitState.Online || u.State == UnitState.AwaitingSync || u.State == UnitState.Standby) &&
            BlackStart == null && GM.Ledger.Pennies >= ServiceCost(u);

        public bool Service(GridUnit u)
        {
            if (!CanService(u) || !GM.Ledger.SpendPennies(ServiceCost(u))) return false;
            u.ResumeOnline = u.State != UnitState.Standby;
            u.SetState(UnitState.InService, ServiceHoursFor(u) * ServiceTime);
            return true;
        }

        public decimal ServiceOverdueCost => OverdueUnits.Aggregate(0m, (sum, u) => sum + ServiceCost(u));

        // "Schedule service for all overdue units (£X)" (bible §14.2.1).
        public int ServiceOverdue()
        {
            int n = 0;
            foreach (var u in OverdueUnits.ToList())
                if (Service(u)) n++;
            return n;
        }

        // Capacity-weighted health of the fleet (bible §11.1 MP): full until half the service
        // interval, nothing at half as much again past it.
        public double MaintenanceScore
        {
            get
            {
                double weight = 0, sum = 0;
                foreach (var u in _units.Where(u => !u.Inherited))
                {
                    double c = u.Capacity(PowerGrid);
                    double health = Math.Clamp(1.5 - u.HoursSinceService / Math.Max(1, u.Definition.serviceIntervalHours), 0, 1);
                    if (u.State == UnitState.Faulted) health = 0;
                    weight += c;
                    sum += c * health;
                }
                return weight > 0 ? 100 * sum / weight : 100;
            }
        }

        // ---- Coal ----

        public double CoalTonnes { get; private set; }
        public float  CoalTargetDays { get; private set; }

        // Tonnes an hour at the last tick's output.
        public double CoalBurnPerHour { get; private set; }

        public double CoalDaysOfCover => CoalBurnPerHour > 0 ? CoalTonnes / (CoalBurnPerHour * 24) : 99;

        public bool HasCoalUnits => _units.Any(u => u.BurnsCoal);

        // Pennies a tonne.
        protected double CoalPrice =>
            (double)Managers.Money.PenniesPerPound * CoalPoundsPerTonne * (Effects != null ? Effects.FuelPriceMultiplier : 1.0);

        public decimal ManualOrderCost => (decimal)(ManualOrderTonnes * CoalPrice);
        private double ManualOrderTonnes => Math.Max(200, Math.Max(CoalBurnPerHour, EstimatedBurnPerHour) * 24 * Data.Era4Data.ManualOrderDays);

        // What the coal units would burn flat out: an order made with nothing running still buys days.
        private double EstimatedBurnPerHour =>
            _units.Where(u => u.BurnsCoal).Sum(u => u.Capacity(PowerGrid) / 1e6 * u.Definition.coalTonnesPerMWh * FuelBurn) * 0.7;

        private static double FuelBurn => Effects != null ? Effects.FuelBurnMultiplier : 1.0;

        public bool OrderCoal()
        {
            if (!IsCurrentEra || !GM.Ledger.SpendPennies(ManualOrderCost)) return false;
            ReceiveCoal(ManualOrderTonnes);
            return true;
        }

        // The fuel manager's target (bible §14.2.2), once it has been introduced.
        public bool FuelManagerOpen => IsIntroduced(Data.Era4Data.FuelScore);

        public void SetCoalTarget(float days) =>
            CoalTargetDays = Math.Clamp(days, 0, Data.Era4Data.MaxTargetDays);

        private void ReceiveCoal(double tonnes)
        {
            CoalTonnes += tonnes;
            foreach (var u in _units.Where(u => u.State == UnitState.NoFuel).ToList()) ReturnToService(u);
        }

        public double FuelScore => 100 * Math.Clamp(CoalDaysOfCover / Data.Era4Data.RecommendedCoverDays, 0, 1);

        // ---- Black-start fitting (redundancy, Era 5 on) ----

        public bool CanFitBlackStart =>
            IsUnlockedFeature(Data.Era5Data.BlackStartId) || IsIntroduced(Data.Era5Data.RedundancyScore);

        // A unit that can't follow the load (a reactor) can't start a dead grid either.
        public GridUnit NextToFit =>
            CanFitBlackStart ? _units.Where(u => !u.Inherited && !u.BlackStartCapable && u.CanFollow && u.State != UnitState.Faulted)
                                     .OrderBy(u => u.Definition.baseCostPennies).FirstOrDefault()
                             : null;

        public decimal FitCost(GridUnit u) => (decimal)(u.Definition.baseCostPennies * Data.Era5Data.BlackStartFitShare);

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

        // ---- Preparedness dashboard ----

        public static bool IsIntroduced(string score) => Introduced.Contains(score);
        private static readonly HashSet<string> Introduced = new();

        public static void LoadIntroduced(IEnumerable<string> scores)
        {
            Introduced.Clear();
            if (scores != null) foreach (var s in scores) Introduced.Add(s);
        }

        public static IEnumerable<string> IntroducedScores => Introduced;

        protected void Introduce(string score, string title, string story)
        {
            if (!Introduced.Add(score)) return;
            EventBus.Publish(new ScoreIntroducedEvent(score, title, story));
        }

        // ---- Frequency alarms and the blackout ----

        public enum Alarm { Normal, Amber, Red }
        public Alarm AlarmLevel =>
            Grid.Dead ? Alarm.Red : Frequency < WarnHz ? Alarm.Red : Frequency < AmberHz ? Alarm.Amber : Alarm.Normal;

        // Real seconds until the cascade (bible §10.2), or −1 when no warning is running.
        public double CascadeSecondsLeft { get; private set; } = -1;

        // So low that units are tripping on their own protection: the countdown runs fast.
        // Only while the load is still more than the units can carry: once the player has
        // dropped enough, the climb back up counts at the normal pace.
        public bool PastTheBrink => !Grid.Dead && Frequency < CollapseHz && Grid.Demand > Grid.OnlineCapacity;

        public BlackStartSession BlackStart { get; private set; }

        // Online capacity beyond what is being drawn: the "RESERVE" line on the board.
        public double ReserveWatts => Math.Max(0, Grid.OnlineCapacity - Grid.Load);

        // A remedy for the warning: the first unit that could be synchronised now.
        public GridUnit ReserveToSync => NextToSync;

        // Era 5: the relays' load shedding, 0 to 3 stages (5, 10, 15 % more each).
        public int SheddingStage { get; private set; }
        private double _calmSeconds;
        public static readonly double[] ShedFractions = { 0, 0.05, 0.15, 0.30 };

        protected override double AreaShape(LoadArea area, double hourOfDay) =>
            base.AreaShape(area, hourOfDay) * (1 - ShedFractions[SheddingStage]);

        // The first brownout introduces the redundancy score (bible §14.2.3).
        private void OnBrownout() =>
            Introduce(Data.Era5Data.RedundancyScore, "Redundancy",
                      "The relays shed load and kept the grid up, but bringing it back showed how few units could start " +
                      "a dead grid on their own. Fit more units for black start.");

        // What led to the last blackout, for the post-mortem and for tuning.
        public string LastBlackoutCause { get; private set; }

        protected void Blackout(bool drill = false)
        {
            LastBlackoutCause = $"{Frequency:0.00} Hz, load {Grid.Load / 1e6:0} MW on {Grid.OnlineCapacity / 1e6:0} MW online, " +
                                $"{(PastTheBrink ? "past the brink" : "the warning ran out")}, last trip: {_lastTrip}";
            var lit = _areas.Where(a => a.Supplied).ToList();
            double lost = lit.Sum(a => a.DemandWatts);
            foreach (var u in _units)
            {
                if (u.Inherited) continue;
                if (u.State == UnitState.Online) u.SetState(UnitState.AwaitingSync);
            }
            AfterBlackout();
            string cause = drill
                ? "A planned drill: the grid is down on purpose, every customer warned and every contract waiting."
                : _lastTrip.StartsWith("forced")
                ? "Switched off from the DEV strip."
                : Grid.Demand > Grid.OnlineCapacity
                    ? $"The load ({NumberFormatter.FormatWatts(Grid.Demand)}) outgrew the {NumberFormatter.FormatWatts(Grid.OnlineCapacity)} " +
                      "online. The frequency fell past recovery and the units tripped one by one."
                    : "The frequency fell past recovery and the units tripped one by one.";
            Grid.Collapse();
            CascadeSecondsLeft = -1;
            SheddingStage = 0;
            BlackStart = new BlackStartSession(this, lit.Select(a => a.Id).ToList(), lost, Faults, cause, drill);
            if (!drill) StopManagers();
            _lastTrip = "none";
            EventBus.Publish(new BlackoutEvent(EraNumber));
        }

        // Era 6: what a dark grid does to its units beyond tripping them (reactors scram).
        protected virtual void AfterBlackout() { }

        // Era 6: the drill's score, once its post-mortem is closed.
        protected internal virtual void AfterBlackStart(BlackStartSession session) { }

        // The black start's clock is real time, whatever the game clock or the DEV speed.
        private void Update()
        {
            if (BlackStart != null && IsCurrentEra) BlackStart.Tick(UnityEngine.Time.unscaledDeltaTime);
        }

        // For the black start: what kind of load an area is, and its name.
        public abstract bool IsIndustrial(string areaId);
        public abstract string AreaName(string areaId);

        // Called by the black start as it brings the grid back.
        internal void RestartFrequency() => Grid.Reset();

        internal void EndBlackStart(float trustLost)
        {
            if (trustLost > 0) GM.Ledger.AddReputation(-trustLost);
            BlackStart = null;
        }

        // Test and DEV hooks: a fixed load, whatever the hour; the blackout as if the cascade had run out.
        public double? LoadOverrideForTests { get; set; }
        public void ForceBlackout()
        {
            if (!IsCurrentEra || BlackStart != null) return;
            _lastTrip = "forced from the DEV strip";
            Blackout();
        }

        // ---- Looking ahead ----

        // What the grid would be asked for at an hour of the day with the areas lit now: each
        // on its daily curve, the lines' losses, and the contracts running.
        public double DemandAt(double hourOfDay)
        {
            double sent = ContractSentWatts;
            foreach (var a in _areas)
            {
                if (!a.Supplied) continue;
                double d = a.BaseWatts * AreaShape(a, hourOfDay);
                sent += d + TransmissionNetwork.EffectiveLoss(a) * d * d;
            }
            return sent;
        }

        // What the contracts running take at the station, before the lines' losses.
        public double ContractSentWatts =>
            GM != null && GM.ContractSupplyFactor > 0 ? GM.Contracts.DeliveredWatts / GM.ContractSupplyFactor : 0;

        // The highest demand over the next game hours, and how many hours away it is: the
        // status strip warns with it before the evening rise outruns the units online.
        public (double Watts, double HoursAhead) PeakAhead(double hours = 6)
        {
            double now = Now / 3600.0;
            double best = 0, at = 0;
            for (double h = 0; h <= hours; h += 0.25)
            {
                double w = DemandAt((now + h) % 24.0);
                if (w > best) { best = w; at = h; }
            }
            return (best, at);
        }

        // Capacity that could be online: the units online plus those waiting to be synchronised
        // (and, in Era 6, those already starting).
        public double ReadyCapacity =>
            _units.Where(u => u.IsOnline || u.State == UnitState.AwaitingSync || u.State == UnitState.Starting)
                  .Sum(u => u.Capacity(PowerGrid)) * Grid.CapacityFactor;

        // ---- The tick ----

        private double _lastDt = 1.0;
        private double _contractRamp;

        protected override double Dispatch(double wantedWatts, double dt, double gameHours)
        {
            var inherited = InheritedUnit;
            if (inherited != null) inherited.InheritedWatts = Managers.EraManager.InheritedWattsFor(EraNumber);

            if (BlackStart != null)
            {
                // The island the black start has built is all that runs.
                foreach (var u in _units) u.OutputWatts = 0;
                return BlackStart.IslandSupply;
            }

            _lastDt = dt;
            // A contract's load comes on over RampSeconds too, like a feeder's: the ledger
            // counts it served from the start, but the grid feels it arrive gradually.
            double contracts = GM.ContractSupplyFactor > 0 ? GM.Contracts.DeliveredWatts / GM.ContractSupplyFactor : 0;
            double step = Math.Max(Grid.OnlineCapacity, 1e6) / RampSeconds * dt;
            _contractRamp = contracts > _contractRamp ? Math.Min(contracts, _contractRamp + step) : contracts;
            wantedWatts += _contractRamp - contracts;
            if (LoadOverrideForTests is double load) wantedWatts = load;
            Grid.GovernorFactor = Effects != null ? Effects.GovernorTimeMultiplier : 1.0;
            Grid.Step(_units, PowerGrid, wantedWatts, dt);
            return Grid.Generation;
        }

        protected override void AfterShare(double gameHours)
        {
            bool away = GM.Contracts.ShortfallPaused; // catching up on time away
            double dt = _lastDt;

            // Coal burned for what was sent.
            double burn = 0;
            foreach (var u in _units)
                if (u.BurnsCoal && u.IsOnline) burn += u.OutputWatts / 1e6 * u.Definition.coalTonnesPerMWh * FuelBurn;
            CoalBurnPerHour = burn;
            CoalTonnes = Math.Max(0, CoalTonnes - burn * gameHours);
            if (CoalTargetDays > 0 && FuelManagerOpen)
            {
                double target = Math.Max(CoalBurnPerHour, EstimatedBurnPerHour) * 24 * CoalTargetDays;
                if (CoalTonnes < target * 0.95)
                {
                    double tonnes = target - CoalTonnes;
                    decimal cost = (decimal)(tonnes * CoalPrice);
                    decimal paid = GM.Ledger.DrainPennies(cost);
                    if (paid > 0) ReceiveCoal(tonnes * (double)(paid / cost));
                }
                // Coal in the yard ties up money (bible §14.2.2: "learns it has an ongoing cost").
                GM.Ledger.DrainPennies((decimal)(CoalTonnes * CoalPrice * Data.Era4Data.CarryingSharePerDay * gameHours / 24));
            }
            if (CoalTonnes <= 0 && !away && _units.Any(u => u.BurnsCoal && u.IsOnline))
            {
                foreach (var u in _units.Where(u => u.BurnsCoal && u.IsOnline).ToList()) u.SetState(UnitState.NoFuel);
                EventBus.Publish(new WorksNoticeEvent("The coal stockpile is empty. Every coal unit has tripped."));
                Introduce(Data.Era4Data.FuelScore, "Fuel reserve",
                          "The bunkers ran dry in the middle of the evening and the coal units went down one after another. " +
                          "Set how many days of coal to keep in the yard: the merchant will keep it topped up.");
                if (CoalTargetDays <= 0) CoalTargetDays = Data.Era4Data.DefaultTargetDays;
            }

            // Service, repairs, and wear.
            foreach (var u in _units.ToList())
            {
                if (u.Inherited) continue;
                if (u.State == UnitState.InService || u.State == UnitState.Faulted)
                {
                    u.HoursLeft -= gameHours;
                    if (u.HoursLeft <= 0)
                    {
                        u.HoursSinceService = 0;
                        ReturnToService(u);
                    }
                    continue;
                }
                // Era 6: a start running up, or a tripped reactor's hold running down. In a black
                // start, units come back through the minigame instead.
                if (u.State == UnitState.Starting && BlackStart == null)
                {
                    u.HoursLeft -= gameHours;
                    if (u.HoursLeft <= 0) u.SetState(UnitState.AwaitingSync);
                    continue;
                }
                if (u.State == UnitState.Standby)
                {
                    u.HoursLeft = Math.Max(0, u.HoursLeft - gameHours);
                    continue;
                }
                if (u.State != UnitState.Online) continue;
                u.HoursSinceService += gameHours;
                // In a black start the start sequence's step failures stand in for wear.
                if (away || BlackStart != null || !IsOverdue(u)) continue;
                double overdue = (u.HoursSinceService - u.Definition.serviceIntervalHours) / u.Definition.serviceIntervalHours;
                double chance = (Data.Era4Data.FailureBasePerHour + Data.Era4Data.FailureSlopePerHour * overdue) * gameHours;
                if (Faults.NextDouble() < chance) Fail(u);
            }

            // Auto-synchroniser: a waiting unit closes itself, on a clean close.
            if (AutoSync && !Grid.Dead && BlackStart == null)
                foreach (var u in AwaitingSync.ToList()) EndSync(u, Synchroscope.Close.Clean);

            if (BlackStart != null || away) { CascadeSecondsLeft = -1; return; }

            // Era 5's relays shed load in stages, and give it back once things are calm.
            if (UnderFrequencyRelays)
            {
                int stage = Frequency < 48.8 ? 3 : Frequency < 49.2 ? 2 : Frequency < 49.5 ? 1 : 0;
                if (stage > SheddingStage)
                {
                    SheddingStage = stage;
                    _calmSeconds = 0;
                    EventBus.Publish(new WorksNoticeEvent($"Under-frequency relays shed {ShedFractions[stage]:0%} of the load."));
                    OnBrownout();
                }
                else if (SheddingStage > 0 && Frequency > 49.85 && ReserveWatts > Grid.Load * 0.06)
                {
                    _calmSeconds += dt;
                    if (_calmSeconds >= 30) { SheddingStage--; _calmSeconds = 0; }
                }
            }

            // The cascade warning (bible §10.2).
            if (Frequency < WarnHz)
            {
                if (CascadeSecondsLeft < 0) CascadeSecondsLeft = CascadeSeconds;
                else CascadeSecondsLeft -= dt * (PastTheBrink ? Data.Era4Data.BrinkRate : 1);
                if (CascadeSecondsLeft <= 0) Blackout();
            }
            else if (Frequency >= ClearHz) CascadeSecondsLeft = -1;
        }

        private string _lastTrip = "none";

        private void Fail(GridUnit u)
        {
            _lastTrip = $"Unit {u.Number} {u.ShortName} failed";
            decimal repair = (decimal)(u.Definition.baseCostPennies * Data.Era4Data.RepairCostShare);
            GM.Ledger.DrainPennies(repair);
            u.ResumeOnline = true;
            u.SetState(UnitState.Faulted, Data.Era4Data.RepairHours * ServiceTime);
            EventBus.Publish(new WorksNoticeEvent($"Unit {u.Number} {u.ShortName} has tripped: overdue for service. Repairs under way."));
            Introduce(Data.Era4Data.MaintenanceScore, "Maintenance",
                      $"Unit {u.Number} ran past its service and failed, and the others had to carry its load. " +
                      "Units that are serviced on time don't fail. Schedule a service for every unit that's overdue.");
        }

        // ---- Shedding by hand (the cascade warning's other remedy) ----

        public bool SetShed(string id, bool shed)
        {
            var a = Area(id);
            if (a == null || !a.Connected || !IsCurrentEra) return false;
            a.Shed = shed;
            return true;
        }

        // The biggest lit area: the one to drop when the frequency is falling. By its full
        // size, not what it draws this second: one just switched in is still coming on.
        public LoadArea BiggestLit => _areas.Where(a => a.Supplied).OrderByDescending(a => a.BaseWatts).FirstOrDefault();

        protected override double OfflineCapacityWatts() =>
            _units.Where(u => u.IsOnline).Sum(u => u.Capacity(PowerGrid));

        // Contracts take from what the online units can make.
        protected override double StandingLossFraction => 0.04;

        // What a new contract could have without starving the grid: the reserve, at the far
        // end of the lines.
        public override double SpareForContracts => ReserveWatts * (GM != null ? GM.ContractSupplyFactor : 1);
    }
}
