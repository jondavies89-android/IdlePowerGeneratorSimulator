using System;
using System.Collections.Generic;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // The grid's heartbeat (bible §2.2, Era 4; §4.1 the swing equation). Online units share
    // the load in proportion to their size, each chasing its share through its governor with
    // a lag. Frequency follows
    //
    //     2H/f0 · df/dt = (P_gen − P_load(f)) / S
    //
    // with the load easing off as frequency falls (P_load = P0·(1 + D·Δf/f0), bible's D), and
    // the operators' frequency bias asking the units for a little more while it is low, which
    // brings it back to 50 Hz whenever there is reserve. Short of capacity, it settles where
    // the load's own easing balances the deficit: about 1 Hz down for every 5 % short.
    //
    // Stepped once per sim tick with dt in real seconds: a grid's swings take seconds, not the
    // game-clock hours its demand curve moves in.
    public class GridDispatch
    {
        public const double Nominal = 50.0;

        // Load self-regulation: 2.5 % of load per 1 % of frequency.
        public const double LoadDamping = 2.5;
        // Operators' frequency bias: per-unit extra generation asked per per-unit of frequency low.
        public const double FrequencyBias = 15.0;

        public double Frequency { get; private set; } = Nominal;
        public double LastFrequency { get; private set; } = Nominal;
        public double Generation { get; private set; }   // ΣP sent by the units
        public double Load { get; private set; }         // what the load drew at this frequency
        public double OnlineCapacity { get; private set; }
        public double Demand { get; private set; }       // what the load asks for at 50 Hz

        // Hz per second, last step: the needle's drift (bible §4.2 RoCoF, never shown by name).
        public double Trend => Frequency - LastFrequency;

        // Load noise (bible §3.6, an Ornstein–Uhlenbeck process at ~0.3 %): the needle never
        // quite sits still.
        private double _noise;
        private readonly SeededRandom _rng;

        // Multiplies every unit's governor lag (governor tuning research makes it quicker).
        public double GovernorFactor { get; set; } = 1.0;

        // Share of each unit's rating left for real power (Era 5): a unit also making reactive
        // power for the cities has √(1 − q²) of its rating to give as real power.
        public double CapacityFactor { get; set; } = 1.0;

        public GridDispatch(SeededRandom rng) { _rng = rng; }

        // Until the first step after a reset the units are already carrying their share: a
        // grid that starts (an era opening, a save loading, a black start finished) starts
        // running, not from a standing start that would crash the frequency in a second.
        private bool _settled;

        // The load asked for on the last step. Changes the operators make or expect (an area
        // switched in, the evening rise) are scheduled: the units follow them directly, and
        // the governors (with their lag) only have to answer surprises — a trip, the noise.
        private double _lastScheduled;

        public void Reset()
        {
            _settled = false;
            Frequency = LastFrequency = Nominal;
            _noise = 0;
            Generation = Load = 0;
        }

        // A dead grid (a blackout): no frequency until it is restarted.
        public void Collapse()
        {
            Frequency = LastFrequency = 0;
            Generation = Load = 0;
        }

        public bool Dead => Frequency <= 0;

        // Steps the grid by dt real seconds against loadAtNominal watts asked for.
        // In quarter steps: the swing and the governors move within a second, and one Euler
        // step a second overshoots a sudden change badly.
        public const int SubSteps = 4;

        public void Step(IReadOnlyList<GridUnit> units, PowerGrid grid, double loadAtNominal, double dt)
        {
            LastFrequency = Frequency;
            Demand = Math.Max(0, loadAtNominal);
            if (Dead) { foreach (var u in units) u.OutputWatts = 0; return; }
            for (int i = 0; i < SubSteps; i++) SubStep(units, grid, loadAtNominal, dt / SubSteps);
        }

        private void SubStep(IReadOnlyList<GridUnit> units, PowerGrid grid, double loadAtNominal, double dt)
        {
            if (Dead) return;

            _noise += -0.2 * _noise * dt + 0.0015 * (_rng.NextDouble() * 2 - 1) * Math.Sqrt(dt);
            double p0 = Math.Max(0, loadAtNominal) * (1 + _noise);

            double capacity = 0, inertia = 0;
            foreach (var u in units)
            {
                if (!u.IsOnline) { u.OutputWatts = 0; continue; }
                double c = u.Capacity(grid) * CapacityFactor;
                capacity += c;
                inertia += c * (u.Inherited ? 4.0 : u.Definition.inertiaSeconds);
            }
            OnlineCapacity = capacity;
            if (capacity <= 0)
            {
                // Nothing turning: the frequency goes with it.
                Generation = 0;
                Load = p0;
                Frequency = Math.Max(0, Frequency - 5 * dt);
                return;
            }
            double h = inertia / capacity;

            double dfPu = (Frequency - Nominal) / Nominal;
            double asked = Math.Clamp(p0 * (1 - FrequencyBias * dfPu), 0, capacity);

            double scheduledChange = _settled ? Math.Max(0, loadAtNominal) - _lastScheduled : 0;
            _lastScheduled = Math.Max(0, loadAtNominal);

            double generation = 0;
            foreach (var u in units)
            {
                if (!u.IsOnline) continue;
                double c = u.Capacity(grid) * CapacityFactor;
                double target = c / capacity * asked;
                u.OutputWatts += c / capacity * scheduledChange;
                double tau = Math.Max(0.5, (u.Inherited ? 8.0 : u.Definition.governorSeconds) * GovernorFactor);
                if (!_settled) u.OutputWatts = target;
                else u.OutputWatts += (target - u.OutputWatts) * (1 - Math.Exp(-dt / tau));
                u.OutputWatts = Math.Clamp(u.OutputWatts, 0, c);
                generation += u.OutputWatts;
            }
            Generation = generation;
            _settled = true;

            double load = p0 * (1 + LoadDamping * dfPu);
            Load = Math.Max(0, load);
            double s = Math.Max(capacity, p0);
            double dfdt = Nominal * (generation - Load) / (2 * h * Math.Max(1, s));
            Frequency = Math.Clamp(Frequency + dfdt * dt, 40.0, 55.0);
        }

        // A sudden change of load (a feeder switched in, an arc furnace starting): the grid
        // feels it before the governors can answer.
        public void Kick(double hz) { if (!Dead) Frequency = Math.Clamp(Frequency + hz, 40.0, 55.0); }
    }
}
