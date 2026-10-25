using System;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // Runtime state of a single generator unit.
    // Degradation, fuel tracking, and trip logic live here.
    public class GeneratorInstance
    {
        public GeneratorDefinition Definition         { get; }
        public bool                IsOnline           { get; private set; } = true;
        public double              CurrentOutputWatts { get; private set; }
        public double              FuelLevel          { get; private set; } = 1.0; // 0–1 normalised
        public float               HoursSinceService  { get; private set; }
        public float               AgeYears           { get; private set; }

        private double _outputMultiplier = 1.0;

        public GeneratorInstance(GeneratorDefinition def)
        {
            Definition         = def;
            CurrentOutputWatts = def.basePowerWatts;
        }

        public void Tick(double deltaTime)
        {
            if (!IsOnline) { CurrentOutputWatts = 0; return; }

            double deltaHours = deltaTime / 3600.0;
            HoursSinceService += (float)deltaHours;
            AgeYears          += (float)(deltaHours / 8760.0);

            if (Definition.fuelConsumptionPerSecond > 0)
            {
                FuelLevel -= Definition.fuelConsumptionPerSecond * deltaTime;
                if (FuelLevel <= 0)
                {
                    FuelLevel = 0;
                    Trip("FUEL EXHAUSTED");
                    return;
                }
            }

            CurrentOutputWatts = Definition.basePowerWatts * _outputMultiplier;
        }

        public void SetOutputMultiplier(double mult) => _outputMultiplier = mult;

        public void Trip(string reason)
        {
            IsOnline           = false;
            CurrentOutputWatts = 0;
            EventBus.Publish(new GeneratorTripEvent(this, reason));
        }

        public void BringOnline()
        {
            if (FuelLevel <= 0) return;
            IsOnline = true;
        }

        // Eras 4–5: a unit's breaker, set by its GridUnit state. Quiet: the unit reports its
        // own trips, with their reasons.
        public void SetOnline(bool online)
        {
            IsOnline = online;
            if (!online) CurrentOutputWatts = 0;
        }

        public void Service()        => HoursSinceService = 0;
        public void Refuel(double v) => FuelLevel = Math.Clamp(FuelLevel + v, 0.0, 1.0);

        // §9.4 maintenance cost per year given the original capex
        public double AnnualMaintenanceCost(double capex) =>
            capex * (Definition.maintenanceBaseRate + Definition.maintenanceAgeRate * AgeYears);
    }
}
