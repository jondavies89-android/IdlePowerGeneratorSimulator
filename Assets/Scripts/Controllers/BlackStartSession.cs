using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;
using UnitState = IdlePowerGen.Simulation.GridUnit.UnitState;

namespace IdlePowerGen.Controllers
{
    // A black start (bible §10.3), from the dark to the post-mortem. Real time throughout: the
    // outage clock always counts, and nothing here is sped up by the game clock (bible §10.6).
    // No way out once it starts (§15.3). How hard it is follows the preparedness scores (§11.2).
    public class BlackStartSession
    {
        public enum Phase { Triage, Cranking, Island, SecondUnit, Synchronise, Restore, PostMortem, Done }

        public sealed class StartUnit
        {
            public string Name;
            public double Watts;
            public GridUnit Unit;     // null for the old station's diesel set
            public bool Crank;        // cranked like Era 1's crank; otherwise run up on its gates
            public string Note;
        }

        public sealed class Step
        {
            public string Name;
            public BlackStartSequence.Kind Kind;
        }

        public Phase Current { get; private set; } = Phase.Triage;
        public double OutageSeconds { get; private set; }
        public double PhaseSeconds { get; private set; }

        private readonly UnitGridController _grid;
        private readonly SeededRandom _rng;
        public IReadOnlyList<string> LitBefore { get; }
        public double LoadBefore { get; }
        public string Cause { get; }

        // ---- Triage and cranking ----
        public const double TriageSeconds = 10;
        public List<StartUnit> StartUnits { get; } = new();
        public StartUnit Chosen { get; private set; }
        // The chosen unit's start: the diesel's crank, or a hydro unit's gates.
        public BlackStartCrank Diesel { get; private set; }
        public BlackStartGates Hydro { get; private set; }

        // ---- The island ----
        public double IslandCapacity { get; private set; }
        // Each area comes back a quarter at a time, substation by substation: a whole city
        // would be more than any black-start unit could pick up at once.
        public const int Chunks = 4;
        public Dictionary<string, int> Energised { get; } = new();
        public double IslandLoad { get; private set; }
        public string LastMessage { get; private set; }
        public int Collapses { get; private set; }
        // Industrial loads draw three times their running load as they start (bible §10.3.3).
        public const double IndustrialInrush = 3.0, HouseholdInrush = 1.3;
        // A pickup's inrush, above its running load, dies away over a few seconds, and the
        // island's frequency dips with it. Pick up the next before it has recovered and the
        // dips stack: past the island's capacity, the frequency is past saving.
        public double Transient { get; private set; }
        public const double RecoverSeconds = 2.0;
        // How far the frequency dips with the island's headroom all taken by inrush.
        public const double DipHz = 1.5;
        // Pickups the under-frequency relays threw back off in restoration.
        public int ReliefTrips { get; private set; }
        private readonly Stack<(string id, double running)> _picked = new();

        // ---- The second unit ----
        public GridUnit SecondUnit { get; private set; }
        public List<Step> Steps { get; } = new()
        {
            new Step { Name = "Auxiliaries powered from the island", Kind = BlackStartSequence.Kind.Breakers },
            new Step { Name = "Lube oil circulating", Kind = BlackStartSequence.Kind.OilPump },
            new Step { Name = "Turbine pre-warming", Kind = BlackStartSequence.Kind.Warming },
        };
        public int StepIndex { get; private set; }
        public BlackStartSequence Sequence { get; private set; }
        public Step CurrentStep => StepIndex < Steps.Count ? Steps[StepIndex] : null;
        public bool StepFailed { get; private set; }
        public double StepFailureChance { get; }
        private bool _bypassed;

        // ---- Synchronising ----
        public Synchroscope Scope { get; private set; }
        public double IslandFrequency { get; private set; } = GridDispatch.Nominal;

        // ---- Restoration ----
        public const double RestoreTarget = 0.95;
        public List<GridUnit> OnIsland { get; } = new();

        // ---- The report ----
        public float TrustLost { get; private set; }
        public double CustomerMinutes { get; private set; }
        public List<string> Factors { get; } = new();

        public BlackStartSession(UnitGridController grid, List<string> litBefore, double loadBefore, SeededRandom rng,
                                 string cause = "The frequency fell past recovery and the units tripped one by one.")
        {
            _grid = grid;
            _rng = rng;
            LitBefore = litBefore;
            LoadBefore = Math.Max(1, loadBefore);
            Cause = cause;
            _lossShare = Math.Clamp(grid.LossFraction, 0, 0.5);

            // Bible §11.2: 0.5 % (prepared) to 12 % (neglected) per step, from maintenance and fuel.
            double prepared = (grid.MaintenanceScore + grid.FuelScore) / 200.0;
            StepFailureChance = 0.005 + (1 - Math.Clamp(prepared, 0, 1)) * 0.115;

            // Every area goes dark; the island lights them one at a time.
            foreach (var a in grid.Areas) if (a.Connected) a.Shed = true;

            var inherited = grid.InheritedUnit;
            StartUnits.Add(new StartUnit
            {
                Name = "The old station's diesel set",
                Watts = inherited != null ? inherited.InheritedWatts * grid.DieselShare : 1e6,
                Crank = true,
                Note = "kept for exactly this · crank it into life",
            });
            foreach (var u in grid.Units.Where(u => !u.Inherited && u.BlackStartCapable && u.State != UnitState.Faulted &&
                                                    u.State != UnitState.InService))
                StartUnits.Add(new StartUnit
                {
                    Name = $"Unit {u.Number} {u.ShortName}",
                    Watts = u.Capacity(SimulationTick.Instance?.Grid),
                    Unit = u,
                    Note = "work the gates to run it up to speed",
                });
        }

        // The units the island could bring on next (healthy, not already on it).
        public IEnumerable<GridUnit> Candidates =>
            _grid.Units.Where(u => !u.Inherited && !OnIsland.Contains(u) && u != Chosen?.Unit &&
                                   u.State != UnitState.Faulted && u.State != UnitState.InService && u.State != UnitState.NoFuel);

        // What the island can supply now, for the grid's dispatch.
        public double IslandSupply => Current >= Phase.Island && Current < Phase.Done ? Math.Min(IslandLoad * 1.02, IslandCapacity) : 0;

        // ---- Time ----

        public void Tick(double dt)
        {
            if (Current == Phase.Done) return;
            if (Current != Phase.PostMortem) OutageSeconds += dt;
            PhaseSeconds += dt;
            Transient *= Math.Exp(-dt / RecoverSeconds);
            switch (Current)
            {
                case Phase.Triage:
                    if (PhaseSeconds >= TriageSeconds) Go(Phase.Cranking);
                    break;
                case Phase.Cranking:
                    Diesel?.Tick(dt);
                    if (Hydro != null)
                    {
                        Hydro.Tick(dt);
                        if (Hydro.Running) Started();
                    }
                    break;
                case Phase.SecondUnit:
                    var step = CurrentStep;
                    if (StepFailed || step == null || Sequence == null) break;
                    Sequence.Tick(step.Kind, dt);
                    if (Sequence.Failed)
                    {
                        StepFailed = true;
                        LastMessage = Sequence.Message;
                    }
                    else if (Sequence.Done(step.Kind)) FinishStep();
                    break;
                case Phase.Synchronise:
                    // An island wanders more than a grid does.
                    IslandFrequency = GridDispatch.Nominal + 0.08 * Math.Sin(OutageSeconds * 0.7);
                    Scope?.Step(IslandFrequency, dt);
                    break;
            }
        }

        private void Go(Phase phase)
        {
            Current = phase;
            PhaseSeconds = 0;
        }

        // ---- Phase 1: cranking power ----

        public void Choose(int index)
        {
            if (Current != Phase.Cranking || index < 0 || index >= StartUnits.Count) return;
            Chosen = StartUnits[index];
            Diesel = Chosen.Crank ? new BlackStartCrank() : null;
            Hydro = Chosen.Crank ? null : new BlackStartGates();
        }

        // A turn of the diesel's crank.
        public void Crank()
        {
            if (Current == Phase.Cranking) Diesel?.Turn();
        }

        // Fires the diesel: it starts if the timing was right.
        public BlackStartCrank.Fire Fire()
        {
            if (Current != Phase.Cranking || Diesel == null) return BlackStartCrank.Fire.NotReady;
            var result = Diesel.TryFire();
            if (result == BlackStartCrank.Fire.Caught) Started();
            return result;
        }

        public void SetGates(double opening)
        {
            if (Current == Phase.Cranking) Hydro?.Set(opening);
        }

        private void Started()
        {
            IslandCapacity = Chosen.Watts;
            if (Chosen.Unit != null) { Chosen.Unit.SetState(UnitState.Online); OnIsland.Add(Chosen.Unit); }
            LastMessage = $"{Chosen.Name} is running: {NumberFormatter.FormatWatts(IslandCapacity)} to build an island on.";
            Transient = 0;
            Go(Phase.Island);
        }

        // ---- Phase 2: the island ----

        public double InrushOf(string areaId)
        {
            var a = _grid.Area(areaId);
            return a.DemandWatts > 0 ? a.DemandWatts * (IsIndustrial(areaId) ? IndustrialInrush : HouseholdInrush) : 0;
        }

        public bool IsIndustrial(string areaId) => _grid.IsIndustrial(areaId);

        public double RunningLoadOf(string areaId)
        {
            var a = _grid.Area(areaId);
            return a.BaseWatts * DemandModel.DailyShape(ContractDesk.Now / 3600.0 % 24.0);
        }

        // Energises an area on the island. Too much, with the inrush, and the island goes down.
        public bool Energise(string areaId)
        {
            if ((Current != Phase.Island && Current != Phase.Restore) || ChunksOn(areaId) >= Chunks) return false;
            var a = _grid.Area(areaId);
            if (a == null || !a.Connected) return false;
            double running = RunningLoadOf(areaId) / Chunks;
            double inrush = running * (IsIndustrial(areaId) ? IndustrialInrush : HouseholdInrush);
            if (IslandLoad + inrush > IslandCapacity)
            {
                if (Current == Phase.Island)
                    Collapse($"{AreaName(areaId)}'s inrush pulled the island down. Start again, and pick smaller loads first.");
                else LastMessage = $"Not enough on the island to pick up {AreaName(areaId)} yet. Bring on another unit.";
                return false;
            }
            if (IslandLoad + Transient + inrush > IslandCapacity)
            {
                // It would have fitted, had the last pickup's dip died away first.
                if (Current == Phase.Island)
                    Collapse($"{AreaName(areaId)} came on before the frequency had recovered, and the dips together " +
                             "pulled the island down. Start again, and let the needle settle between pickups.");
                else ReliefTrip();
                return false;
            }
            a.Shed = false;
            Energised[areaId] = ChunksOn(areaId) + 1;
            IslandLoad += running;
            Transient += inrush - running;
            _picked.Push((areaId, running));
            LastMessage = ChunksOn(areaId) >= Chunks ? $"{AreaName(areaId)} is back." : $"{AreaName(areaId)}: {ChunksOn(areaId)} of {Chunks} substations back.";
            return true;
        }

        // Back to the cranking with everything dark again (bible §10.3.3).
        private void Collapse(string message)
        {
            Collapses++;
            LastMessage = message;
            foreach (var id in Energised.Keys) _grid.Area(id).Shed = true;
            Energised.Clear();
            _picked.Clear();
            IslandLoad = 0;
            Transient = 0;
            foreach (var u in OnIsland) u.SetState(UnitState.AwaitingSync);
            OnIsland.Clear();
            Chosen = null;
            Diesel = null;
            Hydro = null;
            Go(Phase.Cranking);
        }

        // In restoration the island is big enough to ride a deep dip, but the under-frequency
        // relays throw the last substation picked up back off to save it.
        private void ReliefTrip()
        {
            ReliefTrips++;
            Transient *= 0.5;
            if (_picked.Count == 0)
            {
                LastMessage = "The frequency dipped too far. Let it recover before the next pickup.";
                return;
            }
            var (id, running) = _picked.Pop();
            IslandLoad = Math.Max(0, IslandLoad - running);
            if (--Energised[id] <= 0)
            {
                Energised.Remove(id);
                _grid.Area(id).Shed = true;
            }
            LastMessage = $"The frequency dipped too far: the relays threw {AreaName(id)}'s last substation back off. " +
                          "Let it recover between pickups.";
        }

        // The next substation would fit with the island as it is now, the last dip included.
        public bool FitsNow(string areaId)
        {
            if (!Fits(areaId)) return false;
            double running = RunningLoadOf(areaId) / Chunks;
            return IslandLoad + Transient + running * (IsIndustrial(areaId) ? IndustrialInrush : HouseholdInrush) <= IslandCapacity;
        }

        // The island's frequency: down by the share of its headroom the inrush is taking.
        // The governor holds it at 50 Hz once the inrush has died away.
        public double IslandHz
        {
            get
            {
                if (Current == Phase.Synchronise) return IslandFrequency;
                double headroom = IslandCapacity - IslandLoad;
                if (headroom <= 0) return GridDispatch.Nominal - DipHz;
                return GridDispatch.Nominal - DipHz * Math.Min(1, Transient / headroom);
            }
        }

        // The frequency at which the island is past saving.
        public static double CollapseHz => GridDispatch.Nominal - DipHz;

        private string AreaName(string id) => _grid.AreaName(id);

        public int ChunksOn(string id) => Energised.TryGetValue(id, out var n) ? n : 0;

        // The next substation of an area, inrush included, would fit on the island as it is.
        public bool Fits(string areaId)
        {
            if (ChunksOn(areaId) >= Chunks) return false;
            double running = RunningLoadOf(areaId) / Chunks;
            return IslandLoad + running * (IsIndustrial(areaId) ? IndustrialInrush : HouseholdInrush) <= IslandCapacity;
        }

        public bool AnythingFits => LitBefore.Any(Fits);

        // The island is holding: on to the second unit. A start unit too small for even one
        // substation can still power the next unit's auxiliaries.
        public bool CanHold => Current == Phase.Island && (Energised.Count > 0 || !AnythingFits);

        public void IslandHolding()
        {
            if (!CanHold) return;
            SecondUnit = Candidates.OrderByDescending(u => u.Capacity(SimulationTick.Instance?.Grid)).FirstOrDefault();
            if (SecondUnit == null) { Go(Phase.Restore); return; }
            BeginSequence();
            Go(Phase.SecondUnit);
        }

        // ---- Phase 3: the second unit's start sequence ----

        private void BeginSequence()
        {
            StepIndex = 0;
            StepFailed = false;
            Sequence = new BlackStartSequence(_rng);
        }

        // The player's hands for each step.
        public bool CloseAuxiliary(string breaker) =>
            Current == Phase.SecondUnit && !StepFailed && CurrentStep?.Kind == BlackStartSequence.Kind.Breakers &&
            Sequence.CloseBreaker(breaker);

        public void HoldPump(bool held)
        {
            if (Sequence != null) Sequence.PumpHeld = held && Current == Phase.SecondUnit && !StepFailed;
        }

        public void SetSteamValve(double opening)
        {
            if (Current == Phase.SecondUnit && !StepFailed) Sequence?.SetValve(opening);
        }

        // A step done by hand still has to take: neglected plant fails more (bible §11.2).
        private void FinishStep()
        {
            if (!_bypassed && _rng.NextDouble() < StepFailureChance)
            {
                StepFailed = true;
                LastMessage = $"{CurrentStep.Name}: it didn't take.";
                return;
            }
            AdvanceStep();
        }

        private void AdvanceStep()
        {
            _bypassed = false;
            StepIndex++;
            LastMessage = null;
            if (StepIndex >= Steps.Count)
            {
                Scope = new Synchroscope(IslandFrequency, _rng);
                Go(Phase.Synchronise);
            }
        }

        // The step again from its start: the time it takes is the cost.
        public void Retry()
        {
            if (!StepFailed) return;
            StepFailed = false;
            Sequence.Reset(CurrentStep.Kind);
            LastMessage = null;
        }

        // Skips the step, with a chance of damage to the unit later (bible §10.3.4).
        public void Bypass()
        {
            if (!StepFailed) return;
            StepFailed = false;
            _bypassed = true;
            if (_rng.NextDouble() < 0.25) _bypassDamage = true;
            AdvanceStep();
        }
        private bool _bypassDamage;

        // Give the unit up and try the next one, or restore what the island can.
        public void Abandon()
        {
            if (!StepFailed) return;
            SecondUnit.SetState(UnitState.Faulted, Data.Era4Data.RepairHours);
            SecondUnit = Candidates.OrderByDescending(u => u.Capacity(SimulationTick.Instance?.Grid)).FirstOrDefault();
            BeginSequence();
            if (SecondUnit == null) Go(Phase.Restore);
        }

        // ---- Phase 4: synchronisation ----

        public void Faster() => Scope?.Faster();
        public void Slower() => Scope?.Slower();

        public Synchroscope.Close CloseBreaker()
        {
            if (Current != Phase.Synchronise || Scope == null) return Synchroscope.Close.Slammed;
            var result = Scope.Judge();
            if (result == Synchroscope.Close.Slammed)
            {
                SecondUnit.SetState(UnitState.Faulted, Data.Era4Data.SlamRepairHours);
                LastMessage = $"Unit {SecondUnit.Number}'s breaker slammed. Bring up another.";
                SecondUnit = Candidates.OrderByDescending(u => u.Capacity(SimulationTick.Instance?.Grid)).FirstOrDefault();
                BeginSequence();
                Go(SecondUnit != null ? Phase.SecondUnit : Phase.Restore);
                return result;
            }
            BringOn(SecondUnit);
            if (_bypassDamage) SecondUnit.HoursSinceService += SecondUnit.Definition.serviceIntervalHours;
            LastMessage = $"Unit {SecondUnit.Number} is on the island. Now bring the city back.";
            Go(Phase.Restore);
            return result;
        }

        private void BringOn(GridUnit unit)
        {
            unit.SetState(UnitState.Online);
            OnIsland.Add(unit);
            IslandCapacity += unit.Capacity(SimulationTick.Instance?.Grid);
        }

        // ---- Phase 5: restoration ----

        // Units brought on in restoration close on the island without the minigame: the
        // operators have the procedure in hand by now.
        // The old station comes back first: the island is now big enough to start it.
        public bool OldStationBack { get; private set; }

        public bool CanBringOn => Current == Phase.Restore && (!OldStationBack || Candidates.Any());

        public bool BringOnNext()
        {
            if (Current != Phase.Restore) return false;
            if (!OldStationBack && _grid.InheritedUnit != null)
            {
                OldStationBack = true;
                IslandCapacity += _grid.InheritedUnit.InheritedWatts * (1 - (Chosen?.Unit == null ? _grid.DieselShare : 0));
                LastMessage = "The old station is back on the bars.";
                return true;
            }
            var next = Candidates.OrderByDescending(u => u.Capacity(SimulationTick.Instance?.Grid)).FirstOrDefault();
            if (next == null) return false;
            BringOn(next);
            LastMessage = $"Unit {next.Number} is on.";
            return true;
        }

        public double Restored =>
            Energised.Sum(kv => RunningLoadOf(kv.Key) * kv.Value / Chunks) / Math.Max(1, LitBefore.Sum(RunningLoadOf));

        // Everything the island could still do has been done: no unit left to bring on and no
        // substation that would fit. A grid that went dark for want of units (or a unit failed,
        // or the coal ran out) can't get back to 95 %; it comes back with what it can carry.
        public bool OutOfOptions => Current == Phase.Restore && !CanBringOn && !AnythingFits;

        public bool CanFinish => Current == Phase.Restore && (Restored >= RestoreTarget || OutOfOptions);

        // Areas that stayed dark when the grid came back, for want of units to carry them.
        public List<string> LeftDark { get; } = new();

        // The losses on the lines just before the blackout, for what the grid can carry after.
        private readonly double _lossShare;

        public void FinishRestoration()
        {
            if (!CanFinish) return;
            // Every customer still dark is picked up if the units can carry them, smallest
            // first; the rest stay dropped, so the grid doesn't restart short and fall again.
            // The units that will be online when it restarts (the old station always is), with a
            // margin for the noise and Era 5's reactive share of each unit.
            double online = _grid.Units.Where(u => u.IsOnline).Sum(u => u.Capacity(SimulationTick.Instance?.Grid));
            double room = (online * _grid.Grid.CapacityFactor * 0.95 - _grid.ContractSentWatts) / (1 + _lossShare);
            double carried = 0;
            foreach (var id in LitBefore.OrderByDescending(ChunksOn).ThenBy(RunningLoadOf))
            {
                double load = RunningLoadOf(id);
                bool keep = ChunksOn(id) >= Chunks || carried + load <= room;
                _grid.Area(id).Shed = !keep;
                if (keep) carried += load;
                else LeftDark.Add(id);
            }
            double minutes = OutageSeconds; // a real second is a game minute (bible §10.6)
            CustomerMinutes = LoadBefore / 1e6 * minutes;
            TrustLost = (float)Math.Clamp(minutes * 0.05, 1, 12);
            if (_grid.MaintenanceScore < 60) Factors.Add($"Maintenance at {_grid.MaintenanceScore:0}: units were overdue for service.");
            if (_grid.HasCoalUnits && _grid.FuelScore < 60) Factors.Add($"Fuel cover at {_grid.FuelScore:0}: the coal yard was low.");
            if (Collapses > 0) Factors.Add($"The island went down {Collapses} time{(Collapses == 1 ? "" : "s")}: big loads first, " +
                                           "or pickups too close together, are the trap.");
            if (ReliefTrips > 0) Factors.Add($"The relays threw {ReliefTrips} pickup{(ReliefTrips == 1 ? "" : "s")} back off: " +
                                             "the frequency needs a moment between them.");
            if (StartUnits.Count == 1) Factors.Add("Only the old diesel set could start a dead grid.");
            if (LeftDark.Count > 0)
                Factors.Add($"Not enough units to carry {string.Join(", ", LeftDark.Take(3).Select(AreaName))}" +
                            $"{(LeftDark.Count > 3 ? $" and {LeftDark.Count - 3} more" : "")}: left dropped. " +
                            "Pick them up once there's reserve for them.");
            if (Factors.Count == 0) Factors.Add("Not enough spare units online when the load rose.");
            Go(Phase.PostMortem);
        }

        // ---- Phase 6: the post-mortem ----

        public void Close()
        {
            if (Current != Phase.PostMortem) return;
            Go(Phase.Done);
            _grid.RestartFrequency();
            _grid.EndBlackStart(TrustLost);
        }
    }
}
