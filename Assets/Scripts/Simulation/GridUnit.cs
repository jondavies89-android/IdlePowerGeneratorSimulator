using System;
using IdlePowerGen.Models;

namespace IdlePowerGen.Simulation
{
    // One generating unit on an Era 4–5 grid ("UNIT 3 HYDRO 80MW" on the board): its breaker
    // state, what it is sending now, and how long since it was serviced. Wraps the grid's
    // GeneratorInstance, whose online flag is the breaker.
    public class GridUnit
    {
        public enum UnitState
        {
            AwaitingSync, // built or restarted, running up, breaker open
            Online,       // breaker closed, carrying load
            Stopped,      // taken off by the operator
            InService,    // off for maintenance
            Faulted,      // tripped by a failure or a slammed breaker; under repair
            NoFuel,       // a coal unit with the stockpile empty
        }

        public GeneratorInstance Instance { get; }
        public GeneratorDefinition Definition => Instance.Definition;
        public int Number { get; }

        public UnitState State { get; private set; } = UnitState.AwaitingSync;

        // Watts the unit is sending now (the dispatch's share), at most Capacity.
        public double OutputWatts { get; set; }

        // Game hours since its last service; past the interval it may fail (online only).
        public double HoursSinceService { get; set; }

        // Game hours left in service or under repair.
        public double HoursLeft { get; set; }

        // Era 5: fitted to start a dead grid on its own.
        public bool BlackStartCapable { get; set; }

        // Never trips, needs no service or fuel: the inherited station (UNIT 0).
        public bool Inherited { get; }
        // The inherited station's output (the previous era's full output).
        public double InheritedWatts { get; set; }

        public GridUnit(GeneratorInstance instance, int number)
        {
            Instance = instance;
            Number = number;
            Instance.SetOnline(false);
        }

        // The inherited station as a unit of its own.
        public GridUnit(double watts)
        {
            Inherited = true;
            InheritedWatts = watts;
            State = UnitState.Online;
        }

        public bool IsOnline => State == UnitState.Online;

        // What it can send, upgrades included (multiplier from the grid).
        public double Capacity(PowerGrid grid) =>
            Inherited ? InheritedWatts : grid != null ? grid.UnitOutputWatts(Definition) : Definition.basePowerWatts;

        public bool BurnsCoal => !Inherited && Definition.coalTonnesPerMWh > 0;

        public void SetState(UnitState state, double hoursLeft = 0)
        {
            if (Inherited) return;
            State = state;
            HoursLeft = Math.Max(0, hoursLeft);
            Instance.SetOnline(state == UnitState.Online);
            if (state != UnitState.Online) OutputWatts = 0;
        }

        // The breaker closes: the unit picks up load from nothing.
        public void Synchronise() => SetState(UnitState.Online);

        public string ShortName =>
            Inherited ? "OLD STATION" : Definition.displayName.ToUpperInvariant();
    }
}
