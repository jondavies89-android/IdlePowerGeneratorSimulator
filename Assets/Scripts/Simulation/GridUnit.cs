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
            // Era 6 (saved by name, so append only):
            Standby,      // cold and off the bars; HoursLeft > 0 means it can't be started yet
            Starting,     // running up from standby; HoursLeft until it can be synchronised
        }

        // Era 6: what an online unit is told to do (bible Appendix A, Era 6: BASE and AGC).
        public enum UnitMode
        {
            Agc,  // follows the load, within its ramp rate, cheapest to run first
            Base, // held at full output, whatever the load does
        }

        public UnitMode Mode { get; set; } = UnitMode.Agc;

        // Era 6: pounds per MWh to run it now, fuel included. The dispatch loads the units on
        // AGC cheapest first (the merit order); set by the era each tick.
        public double MeritCost { get; set; }

        public GeneratorInstance Instance { get; }
        public GeneratorDefinition Definition => Instance.Definition;
        public int Number { get; }

        public UnitState State { get; private set; } = UnitState.AwaitingSync;

        // Watts the unit is sending now (the dispatch's share), at most Capacity.
        public double OutputWatts { get; set; }

        // Era 6: where the dispatch has it, before its primary response to the frequency, and
        // watts a second that moved on the last step (RMP↑ / RMP↓ on the board).
        public double Scheduled { get; set; }
        public double Rise { get; set; }

        // Game hours since its last service; past the interval it may fail (online only).
        public double HoursSinceService { get; set; }

        // Game hours left in service or under repair.
        public double HoursLeft { get; set; }

        // Era 5: fitted to start a dead grid on its own.
        public bool BlackStartCapable { get; set; }

        // Era 6: back from a service or repair, it starts up again (it was running) rather
        // than going back to standby (it was serviced there).
        public bool ResumeOnline { get; set; } = true;

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

        // Era 6: the inherited fleet follows the load like a unit on AGC, a little faster than coal.
        public const float InheritedRampPerMinute = 0.03f;

        public double RampPerMinute => Inherited ? InheritedRampPerMinute : Definition.rampPerMinute;
        public double MinStableShare => Inherited ? 0 : Definition.minStableShare;
        public bool CanFollow => Inherited || Definition.canFollow;
        // On AGC (and able to follow): the dispatch may move it.
        public bool Following => CanFollow && Mode == UnitMode.Agc;

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
            if (state != UnitState.Online) OutputWatts = Scheduled = 0;
        }

        // The breaker closes: the unit picks up load from nothing.
        public void Synchronise() => SetState(UnitState.Online);

        public string ShortName =>
            Inherited ? "OLD STATION" : Definition.displayName.ToUpperInvariant();
    }
}
