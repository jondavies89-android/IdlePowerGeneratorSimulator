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
            public bool Crank;        // tapped up like Era 1's crank; otherwise the gates are held open
            public string Note;
        }

        public sealed class Step
        {
            public string Name;
            public double Seconds;
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
        public int TapsNeeded { get; private set; }
        public int Taps { get; private set; }
        public double HeldSeconds { get; private set; }
        public const double HydroHoldSeconds = 6;

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

        // ---- The second unit ----
        public GridUnit SecondUnit { get; private set; }
        public List<Step> Steps { get; } = new()
        {
            new Step { Name = "Auxiliaries powered from the island", Seconds = 5 },
            new Step { Name = "Lube oil circulating", Seconds = 3 },
            new Step { Name = "Turbine pre-warming", Seconds = 10 },
        };
        public int StepIndex { get; private set; }
        public double StepElapsed { get; private set; }
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
                    Note = "hold the gates open to spin it up",
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
            switch (Current)
            {
                case Phase.Triage:
                    if (PhaseSeconds >= TriageSeconds) Go(Phase.Cranking);
                    break;
                case Phase.SecondUnit:
                    if (StepFailed || StepIndex >= Steps.Count) break;
                    StepElapsed += dt;
                    if (StepElapsed >= Steps[StepIndex].Seconds)
                    {
                        if (!_bypassed && _rng.NextDouble() < StepFailureChance)
                        {
                            StepFailed = true;
                            LastMessage = $"{Steps[StepIndex].Name}: it didn't take.";
                            break;
                        }
                        _bypassed = false;
                        StepIndex++;
                        StepElapsed = 0;
                        if (StepIndex >= Steps.Count)
                        {
                            Scope = new Synchroscope(IslandFrequency, _rng);
                            Go(Phase.Synchronise);
                        }
                    }
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
            TapsNeeded = _rng.NextInt(8, 13); // 8–12 turns (bible §10.3.2)
            Taps = 0;
            HeldSeconds = 0;
        }

        public void Crank()
        {
            if (Current != Phase.Cranking || Chosen == null || !Chosen.Crank) return;
            if (++Taps >= TapsNeeded) Started();
        }

        public void HoldGates(double dt)
        {
            if (Current != Phase.Cranking || Chosen == null || Chosen.Crank) return;
            HeldSeconds += dt;
            if (HeldSeconds >= HydroHoldSeconds) Started();
        }

        private void Started()
        {
            IslandCapacity = Chosen.Watts;
            if (Chosen.Unit != null) { Chosen.Unit.SetState(UnitState.Online); OnIsland.Add(Chosen.Unit); }
            LastMessage = $"{Chosen.Name} is running: {NumberFormatter.FormatWatts(IslandCapacity)} to build an island on.";
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
                Collapses++;
                if (Current == Phase.Island)
                {
                    // Back to the cranking with everything dark again (bible §10.3.3).
                    LastMessage = $"{AreaName(areaId)}'s inrush pulled the island down. Start again, and pick smaller loads first.";
                    foreach (var id in Energised.Keys) _grid.Area(id).Shed = true;
                    Energised.Clear();
                    IslandLoad = 0;
                    foreach (var u in OnIsland) u.SetState(UnitState.AwaitingSync);
                    OnIsland.Clear();
                    Chosen = null;
                    Go(Phase.Cranking);
                }
                else LastMessage = $"Not enough on the island to pick up {AreaName(areaId)} yet. Bring on another unit.";
                return false;
            }
            a.Shed = false;
            Energised[areaId] = ChunksOn(areaId) + 1;
            IslandLoad += running;
            LastMessage = ChunksOn(areaId) >= Chunks ? $"{AreaName(areaId)} is back." : $"{AreaName(areaId)}: {ChunksOn(areaId)} of {Chunks} substations back.";
            return true;
        }

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
            StepIndex = 0;
            StepElapsed = 0;
            StepFailed = false;
            Go(Phase.SecondUnit);
        }

        // ---- Phase 3: the second unit's start sequence ----

        public void Retry()
        {
            if (!StepFailed) return;
            StepFailed = false;
            StepElapsed = -60; // a minute's penalty (bible §10.3.4), on top of the step again
        }

        // Faster, with a chance of damage to the unit later (bible §10.3.4).
        public void Bypass()
        {
            if (!StepFailed) return;
            StepFailed = false;
            _bypassed = true;
            StepElapsed = Steps[StepIndex].Seconds;
            if (_rng.NextDouble() < 0.25) _bypassDamage = true;
        }
        private bool _bypassDamage;

        // Give the unit up and try the next one, or restore what the island can.
        public void Abandon()
        {
            if (!StepFailed) return;
            SecondUnit.SetState(UnitState.Faulted, Data.Era4Data.RepairHours);
            SecondUnit = Candidates.OrderByDescending(u => u.Capacity(SimulationTick.Instance?.Grid)).FirstOrDefault();
            StepIndex = 0;
            StepElapsed = 0;
            StepFailed = false;
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
                StepIndex = 0;
                StepElapsed = 0;
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
            if (Collapses > 0) Factors.Add($"The island went down {Collapses} time{(Collapses == 1 ? "" : "s")}: big loads first are the trap.");
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
