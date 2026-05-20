using System;
using System.Collections.Generic;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // Central power-state object. Updated by SimulationTick at 1 Hz (§8.1).
    // Era 1: no demand, grid is purely a joule accumulator.
    // Era 2+: demand model is active; surplus/deficit tracked.
    public class PowerGrid
    {
        public double TotalOutputWatts { get; private set; }
        public double TotalDemandWatts { get; private set; }
        public double StoredJoules     { get; private set; }
        public double GridFrequency    { get; private set; } = 50.0;

        private readonly List<GeneratorInstance> _generators = new();
        public IReadOnlyList<GeneratorInstance> Generators => _generators;

        public void AddGenerator(GeneratorInstance gen)    => _generators.Add(gen);
        public bool RemoveGenerator(GeneratorInstance gen) => _generators.Remove(gen);

        public void Tick(double deltaTime, double demandWatts)
        {
            TotalDemandWatts = demandWatts;
            TotalOutputWatts = 0;

            foreach (var gen in _generators)
            {
                gen.Tick(deltaTime);
                TotalOutputWatts += gen.CurrentOutputWatts;
            }

            double surplus = TotalOutputWatts - TotalDemandWatts;
            StoredJoules = Math.Max(0, StoredJoules + surplus * deltaTime);

            UpdateFrequency(surplus, deltaTime);
        }

        // Called directly by tap interaction — adds tap-generated energy to the pool.
        public void AddTapEnergy(double joules) => StoredJoules += joules;

        public bool SpendJoules(double amount)
        {
            if (StoredJoules < amount) return false;
            StoredJoules -= amount;
            return true;
        }

        public void SetStoredJoules(double joules) => StoredJoules = joules;

        // Simplified swing equation (§4.1). Full model activates in Era 4.
        private void UpdateFrequency(double surplusWatts, double deltaTime)
        {
            const double nominal = 50.0;
            const double H       = 5.0;  // system inertia constant (seconds)
            const double D       = 0.1;  // damping coefficient

            if (TotalOutputWatts < 1.0) { GridFrequency = nominal; return; }

            double rocof = surplusWatts / (2.0 * H * TotalOutputWatts);
            GridFrequency += rocof * deltaTime;
            GridFrequency += D * (nominal - GridFrequency) * deltaTime;
            GridFrequency  = Math.Clamp(GridFrequency, 47.0, 53.0);
        }
    }
}
