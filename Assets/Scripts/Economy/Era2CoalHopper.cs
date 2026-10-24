using System;

namespace IdlePowerGen.Economy
{
    // The coal store the steam engines burn from (bible Appendix A, Era 2: "the coal hopper
    // warning introduces fuel logistics in miniature"). Coal is measured in the pennies it
    // cost, so "order 90 d" refills exactly the 90 d that has been burned.
    public class Era2CoalHopper
    {
        // The hopper is sized to a day's burn, so a bigger fleet doesn't mean ordering
        // more often: one order lasts a game day (24 real minutes) whatever the fleet.
        public const double HoursHeld = 24.0;
        // Room for a day of one small engine, so the first engine arrives to a real hopper.
        public const double MinCapacity = 24.0;
        // Todo §4.1: amber below 20 %, red below 5 %.
        public const float LowFraction  = 0.2f;
        public const float VeryLowFraction = 0.05f;

        public double Level { get; private set; }

        public static double Capacity(double burnPerHour) => Math.Max(MinCapacity, burnPerHour * HoursHeld);

        public float Fraction(double burnPerHour) => (float)Math.Clamp(Level / Capacity(burnPerHour), 0.0, 1.0);

        public bool IsEmpty => Level <= 0;

        // Game hours of burn left; infinite with nothing burning.
        public double HoursLeft(double burnPerHour) =>
            burnPerHour > 0 ? Level / burnPerHour : double.PositiveInfinity;

        // Burns for gameHours; false once there is nothing left to burn.
        public bool Burn(double burnPerHour, double gameHours)
        {
            if (burnPerHour <= 0) return true;
            Level = Math.Max(0, Level - burnPerHour * gameHours);
            return Level > 0;
        }

        // What filling it to the brim costs now.
        public decimal OrderCost(double burnPerHour) =>
            (decimal)Math.Ceiling(Math.Max(0, Capacity(burnPerHour) - Level));

        public bool Order(double burnPerHour, CurrencyLedger ledger)
        {
            decimal cost = OrderCost(burnPerHour);
            if (cost <= 0 || !ledger.SpendPennies(cost)) return false;
            Level = Capacity(burnPerHour);
            return true;
        }

        public void Fill(double burnPerHour) => Level = Capacity(burnPerHour);

        public void SetFromSave(double level) => Level = Math.Max(0, level);
    }
}
