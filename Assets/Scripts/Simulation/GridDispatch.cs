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

        // Era 6: big machines are slow (bible §2.2). Units on BASE run flat out; units on AGC
        // are loaded cheapest first (the merit order), each no faster than its ramp rate and
        // never below its minimum stable output. The load the fleet can't follow moves the
        // frequency: down when the evening rise outruns the ramps, up when the night trough
        // falls below what BASE and the minimums are sending.
        public bool MeritOrder { get; set; }

        // How fast a unit on BASE rises to full after it is synchronised, as a share of its
        // rating a game minute, when its own ramp rate is slower (a reactor's is nil on AGC).
        public const double BaseRise = 0.02;

        // Primary response (bible §4.3, 2–30 s, governor droop): every unit that can follow the
        // load answers the frequency at once, a tenth of its rating for 0.2 Hz off, whatever its
        // ramp rate. It holds the needle while the ramps catch up; past that tenth, it falls.
        public const double PrimaryBand = 0.1;
        public const double Droop = 0.04;

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

            if (MeritOrder)
            {
                Generation = MeritShare(units, grid, asked, dfPu, dt);
                _settled = true;
                Swing(Generation, p0, dfPu, capacity, h, dt);
                return;
            }

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
            Swing(generation, p0, dfPu, capacity, h, dt);
        }

        // The swing equation (bible §4.1), with the load easing off as the frequency falls.
        private void Swing(double generation, double p0, double dfPu, double capacity, double h, double dt)
        {
            double load = p0 * (1 + LoadDamping * dfPu);
            Load = Math.Max(0, load);
            double s = Math.Max(capacity, p0);
            double dfdt = Nominal * (generation - Load) / (2 * h * Math.Max(1, s));
            Frequency = Math.Clamp(Frequency + dfdt * dt, 40.0, 55.0);
        }

        // Era 6's dispatch: BASE units climb to full; the rest of what is asked goes to the
        // units on AGC in merit order, each within what its ramp lets it reach this step. On
        // top of those scheduled positions, every unit that can follow gives its primary
        // response to the frequency. Returns what the units send.
        private double MeritShare(IReadOnlyList<GridUnit> units, PowerGrid grid, double asked, double dfPu, double dt)
        {
            double need = asked;
            _followers.Clear();
            _online.Clear();
            foreach (var u in units)
            {
                if (!u.IsOnline) continue;
                _online.Add(u);
                if (u.Following) { _followers.Add(u); continue; }
                double c = u.Capacity(grid) * CapacityFactor;
                double before = u.Scheduled;
                double rise = Math.Max(u.RampPerMinute, BaseRise) * c * dt;
                u.Scheduled = _settled ? Math.Clamp(u.Scheduled + rise, 0, c) : c;
                u.Rise = (u.Scheduled - before) / Math.Max(1e-6, dt);
                need -= u.Scheduled;
            }
            // Cheapest to run first; the unit number breaks ties so the order never flickers.
            _followers.Sort((a, b) => a.MeritCost != b.MeritCost ? a.MeritCost.CompareTo(b.MeritCost) : a.Number.CompareTo(b.Number));

            // Every follower at the least it can reach, then the rest of the need in merit order.
            int n = _followers.Count;
            if (_lo.Length < n) { _lo = new double[n * 2]; _hi = new double[n * 2]; }
            for (int i = 0; i < n; i++)
            {
                var u = _followers[i];
                double c = u.Capacity(grid) * CapacityFactor;
                double floor = u.MinStableShare * c;
                if (_settled)
                {
                    double step = u.RampPerMinute * c * dt;
                    // A unit below its floor (just synchronised) climbs to it at its ramp rate.
                    _lo[i] = Math.Min(c, Math.Max(Math.Min(floor, u.Scheduled + step), u.Scheduled - step));
                    _hi[i] = Math.Min(c, u.Scheduled + step);
                }
                else { _lo[i] = floor; _hi[i] = c; }
                _hi[i] = Math.Max(_hi[i], _lo[i]);
                need -= _lo[i];
            }
            for (int i = 0; i < n; i++)
            {
                var u = _followers[i];
                double add = Math.Clamp(need, 0, _hi[i] - _lo[i]);
                need -= add;
                double before = u.Scheduled;
                u.Scheduled = _lo[i] + add;
                u.Rise = (u.Scheduled - before) / Math.Max(1e-6, dt);
            }

            // Primary response, proportional to how far the frequency is off, within the band.
            double primary = Math.Clamp(-dfPu / Droop, -PrimaryBand, PrimaryBand);
            double generation = 0;
            foreach (var u in _online)
            {
                double c = u.Capacity(grid) * CapacityFactor;
                double extra = u.CanFollow ? primary * c : 0;
                u.OutputWatts = Math.Clamp(u.Scheduled + extra, 0, c);
                generation += u.OutputWatts;
            }
            return generation;
        }

        private readonly List<GridUnit> _followers = new(), _online = new();
        private double[] _lo = new double[16], _hi = new double[16];

        // A sudden change of load (a feeder switched in, an arc furnace starting): the grid
        // feels it before the governors can answer.
        public void Kick(double hz) { if (!Dead) Frequency = Math.Clamp(Frequency + hz, 40.0, 55.0); }
    }
}
