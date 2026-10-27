using System;

namespace IdlePowerGen.Controllers
{
    // A hydro unit spun up from dead by its wicket gates (bible §10.3.2). The player sets the
    // gate opening; the water follows a moment behind, so a turbine run up with the gates
    // wide open keeps accelerating after they're eased back. Open wide to get it moving, ease
    // off early, and hold it at rated speed. Overshoot and the governor trips on overspeed.
    public class BlackStartGates
    {
        // The water column's lag behind the gates: what makes overshoot the danger.
        public const double WaterLagSeconds = 1.2;
        public const double Acceleration = 0.5;
        // The opening that holds the turbine at rated speed, unloaded.
        public const double HoldOpening = 0.4;
        public const double RatedLow = 0.97, RatedHigh = 1.03;
        public const double Overspeed = 1.12;
        public const double HoldSeconds = 3.0;
        public const double TripSeconds = 2.0;

        public double Opening { get; private set; }
        public double Flow { get; private set; }
        public double Speed { get; private set; }
        public double AtSpeed { get; private set; }
        public double Tripped { get; private set; }
        public string Message { get; private set; }

        public bool AtRated => Speed >= RatedLow && Speed <= RatedHigh;
        public bool Running => AtSpeed >= HoldSeconds;

        public void Set(double opening)
        {
            if (Tripped > 0) return;
            Opening = Math.Clamp(opening, 0, 1);
        }

        public void Tick(double dt)
        {
            bool tripped = Tripped > 0;
            if (tripped)
            {
                Tripped = Math.Max(0, Tripped - dt);
                Opening = 0;
            }
            Flow += (Opening - Flow) * Math.Min(1, dt / WaterLagSeconds);
            Speed = Math.Max(0, Speed + Acceleration * (Flow - HoldOpening * Speed) * dt);
            if (!tripped && Speed > Overspeed)
            {
                Tripped = TripSeconds;
                Opening = 0;
                AtSpeed = 0;
                Message = "Overspeed! The governor tripped and shut the gates. Open them again, and ease off sooner.";
                return;
            }
            AtSpeed = !tripped && AtRated ? AtSpeed + dt : 0;
        }
    }
}
