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

        // Global multiplier applied to all generators (raised by upgrades).
        public double GlobalOutputMultiplier { get; set; } = 1.0;

        // Virtual passive watts added to the grid each tick (e.g. UpgradeEffect.AddPassiveWatts).
        public double VirtualPassiveWatts { get; set; }

        // The previous era's works, carried over as one steady station at that era's full
        // output. Added after the multipliers: the new era's upgrades improve the new era's
        // machines, not the old station.
        public double InheritedWatts { get; set; }

        // Era 1's crank, flywheel and water wheel feed the grid only in Era 1; from Era 2 the
        // workshop is part of the inherited station.
        public bool SourcesEnabled { get; set; } = true;

        private readonly List<GeneratorInstance> _generators = new();
        public IReadOnlyList<GeneratorInstance> Generators => _generators;

        // Energy left over after demand on the most recent tick. The economy credits the
        // positive part to the player's spendable joules.
        public double LastTickSurplusJoules { get; private set; }

        // Non-generator sources (Era 1's flywheel and water wheel). Not cleared with the generators: they
        // belong to the scene, not to the save.
        private readonly List<IPowerSource> _sources = new();

        public void AddGenerator(GeneratorInstance gen)    => _generators.Add(gen);
        public bool RemoveGenerator(GeneratorInstance gen) => _generators.Remove(gen);
        public void ClearGenerators()                      => _generators.Clear();
        public void AddSource(IPowerSource source)         => _sources.Add(source);

        // Output the grid is delivering right now, without advancing time.
        public double ComputeOutputWatts()
        {
            double sum = FleetWatts();
            if (SourcesEnabled) foreach (var s in _sources) sum += s.CurrentOutputWatts;
            return sum * GlobalOutputMultiplier + VirtualPassiveWatts + InheritedWatts;
        }

        // Output the grid would keep delivering untended. Offline progress pays on this,
        // straight after a load and before the first tick has run.
        public double ComputeSustainedOutputWatts()
        {
            double sum = FleetWatts();
            if (SourcesEnabled) foreach (var s in _sources) sum += s.SustainedOutputWatts;
            return sum * GlobalOutputMultiplier + VirtualPassiveWatts + InheritedWatts;
        }

        // Surplus energy produced since the last tick that the next tick will credit.
        // The UI adds it to the stored charge so the reading climbs between 1 Hz ticks.
        public double ComputePendingJoules(double secondsIntoTick)
        {
            double sources = 0;
            if (SourcesEnabled) foreach (var s in _sources) sources += s.PendingJoules(secondsIntoTick);
            double produced = (FleetWatts() * secondsIntoTick + sources) * GlobalOutputMultiplier
                              + (VirtualPassiveWatts + InheritedWatts) * secondsIntoTick;
            return Math.Max(0, produced - TotalDemandWatts * secondsIntoTick);
        }

        private double FleetWatts()
        {
            double sum = 0;
            foreach (var gen in _generators)
                if (gen.IsOnline) sum += gen.Definition.basePowerWatts * TypeMultiplier(gen.Definition.generatorType);
            return sum;
        }

        // Upgrades that improve one kind of machine (all apprentices, all steam engines),
        // including the ones bought after the upgrade.
        private readonly Dictionary<Models.GeneratorType, double> _typeMultipliers = new();

        public double TypeMultiplier(Models.GeneratorType type) =>
            _typeMultipliers.TryGetValue(type, out var m) ? m : 1.0;

        public void MultiplyType(Models.GeneratorType type, double factor) =>
            _typeMultipliers[type] = TypeMultiplier(type) * factor;

        public void ClearTypeMultipliers() => _typeMultipliers.Clear();

        // What one running unit of a generator delivers, every multiplier included.
        public double UnitOutputWatts(Models.GeneratorDefinition def) =>
            def.basePowerWatts * TypeMultiplier(def.generatorType) * GlobalOutputMultiplier;

        public void Tick(double deltaTime, double demandWatts)
        {
            TotalDemandWatts = demandWatts;
            TotalOutputWatts = 0;

            foreach (var gen in _generators)
            {
                gen.SetOutputMultiplier(TypeMultiplier(gen.Definition.generatorType));
                gen.Tick(deltaTime);
                TotalOutputWatts += gen.CurrentOutputWatts;
            }
            foreach (var s in _sources)
            {
                // Still ticked from Era 2 so a flywheel left spinning runs down rather than
                // waiting, frozen, for a return to Era 1 that never comes.
                double w = s.Tick(deltaTime);
                if (SourcesEnabled) TotalOutputWatts += w;
            }

            TotalOutputWatts = TotalOutputWatts * GlobalOutputMultiplier + VirtualPassiveWatts + InheritedWatts;

            double surplus = TotalOutputWatts - TotalDemandWatts;
            LastTickSurplusJoules = surplus * deltaTime;
            StoredJoules = Math.Max(0, StoredJoules + LastTickSurplusJoules);

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
