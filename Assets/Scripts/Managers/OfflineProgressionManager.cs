using System;

namespace IdlePowerGen.Managers
{
    // §7 — Tiered offline progression.
    // Short (<5 min): full-fidelity catch-up via SimulationTick.CatchUpOffline.
    // Medium (5 min–6 h): call ComputeMesoscopic.
    // Long (>6 h): call ComputeMacroscopic for algebraic integral.
    public static class OfflineProgressionManager
    {
        public enum OfflineTier { Short, Medium, Long }

        public static OfflineTier ClassifyAbsence(double secondsElapsed)
        {
            if (secondsElapsed < 300)    return OfflineTier.Short;
            if (secondsElapsed < 21600)  return OfflineTier.Medium;
            return OfflineTier.Long;
        }

        // §7.2 offline efficiency at a given elapsed time
        public static double OfflineEfficiency(double elapsedHours, float eraFactor, float tauHours)
        {
            return 0.7 * Math.Exp(-elapsedHours / tauHours) * eraFactor;
        }

        // Medium absence: average the efficiency over the absence window and integrate.
        public static double ComputeMesoscopic(
            double onlineWatts, double absenceSeconds, float eraFactor, float tauHours)
        {
            double elapsedHours = absenceSeconds / 3600.0;
            // Use midpoint of window for average efficiency
            double avgEff = OfflineEfficiency(elapsedHours / 2.0, eraFactor, tauHours);
            return onlineWatts * avgEff * absenceSeconds;
        }

        // Long absence: integrate efficiency curve analytically.
        // ∫₀ᵀ 0.7 * exp(-t/τ) * eraFactor dt = 0.7 * eraFactor * τ * (1 - exp(-T/τ))
        public static double ComputeMacroscopic(
            double onlineWatts, double absenceSeconds, float eraFactor, float tauHours)
        {
            double T   = absenceSeconds / 3600.0;
            double tau = tauHours;
            double integralHours = 0.7 * eraFactor * tau * (1.0 - Math.Exp(-T / tau));
            return onlineWatts * integralHours * 3600.0; // → joules
        }

        // Entry point: returns joules to award for an absence.
        // Caller is responsible for full-fidelity tick (Short tier).
        public static double ComputeOfflineJoules(
            double onlineWatts, double absenceSeconds, float eraFactor, float tauHours)
        {
            switch (ClassifyAbsence(absenceSeconds))
            {
                case OfflineTier.Short:
                    return 0; // handled by SimulationTick.CatchUpOffline
                case OfflineTier.Medium:
                    return ComputeMesoscopic(onlineWatts, absenceSeconds, eraFactor, tauHours);
                default:
                    return ComputeMacroscopic(onlineWatts, absenceSeconds, eraFactor, tauHours);
            }
        }
    }
}
