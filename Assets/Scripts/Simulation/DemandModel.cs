using System;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // Implements §3: D(t) = D_base * S_day(t) * S_week(t) + N(t)
    // Era 1 has no customers, so Evaluate() returns 0.
    // Era 2 activates the daily curve and noise.
    // Annual seasonality (§3.2) and growth (§3.7) are deferred to Era 3+.
    public class DemandModel
    {
        public double BaseDemandWatts { get; set; }

        // Ornstein-Uhlenbeck noise state (§3.6)
        private double _noiseN;
        private readonly SeededRandom _rng;

        // §3.4 daily curve parameters
        private const double Alpha       = 0.15;
        private const double Beta        = 0.25;
        private const double Gamma       = 0.10;
        private const double PhiMorning  = 7.0;  // morning ramp centre (hour)
        private const double PhiEvening  = 17.5; // evening peak centre
        private const double TPeak       = 18.0; // Gaussian spike centre
        private const double SigmaPeak   = 1.5;

        // §3.3 weekly multipliers (Mon–Sun, industrial-leaning city)
        private static readonly double[] WeekMultiplier =
            { 1.05, 1.05, 1.05, 1.05, 1.05, 0.85, 0.75 };

        // §3.6 OU process parameters
        private const double OuTheta = 0.5;   // mean-reversion speed (per in-game hour)
        private const double OuSigma = 0.01;  // ~1 % RMS noise

        public DemandModel(double baseDemandWatts, SeededRandom rng)
        {
            BaseDemandWatts = baseDemandWatts;
            _rng            = rng;
        }

        // gameTimeSeconds = elapsed in-game seconds from epoch.
        // era = current era number; demand is zero in Era 1.
        public double Evaluate(double gameTimeSeconds, float era)
        {
            if (era < 2f) return 0.0;

            double hourOfDay = (gameTimeSeconds / 3600.0) % 24.0;
            int    dayOfWeek = (int)((gameTimeSeconds / 86400.0) % 7.0);

            double sDay  = DailyShape(hourOfDay);
            double sWeek = WeekMultiplier[Math.Clamp(dayOfWeek, 0, 6)];

            // Tick OU noise (1 Hz tick, so dt = 1 in-game second)
            double dtHours = 1.0 / 3600.0;
            double dW      = _rng.NextDouble() * 2.0 - 1.0; // white noise sample
            _noiseN += -OuTheta * _noiseN * dtHours + OuSigma * dW;

            return BaseDemandWatts * sDay * sWeek * (1.0 + _noiseN);
        }

        // §3.4 — gentle morning ramp + sharp evening peak + Gaussian 5–8 pm spike.
        // Averages very nearly 1 over a day (≈ 0.6 before dawn, ≈ 1.4 at the lamp-lighting peak).
        public static double DailyShape(double h)
        {
            return 1.0
                + Alpha * Math.Sin(2 * Math.PI * (h - PhiMorning) / 24.0)
                + Beta  * Math.Sin(2 * Math.PI * (h - PhiEvening) / 12.0)
                + Gamma * Math.Exp(-Math.Pow((h - TPeak) / SigmaPeak, 2));
        }
    }
}
