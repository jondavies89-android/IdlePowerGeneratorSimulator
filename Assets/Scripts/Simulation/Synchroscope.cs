using System;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // Bringing a unit onto a live grid (bible Appendix A, Era 4: "match frequency and phase to
    // close the breaker"). The synchroscope's needle turns at the slip — the difference
    // between the incoming unit's frequency and the grid's — and stands at 12 o'clock when the
    // two are in phase. The operator nudges the unit's governor faster or slower to slow the
    // needle, and closes the breaker as it comes round to the top.
    public class Synchroscope
    {
        public enum Close { Clean, Rough, Slammed }

        // Within these the close is clean: the unit picks up load without a jolt.
        public const double CleanPhaseDegrees = 15, CleanSlipHz = 0.12;
        // Within these it holds, with a jolt to the grid; outside them the breaker slams.
        public const double RoughPhaseDegrees = 35, RoughSlipHz = 0.3;
        public const double NudgeHz = 0.04;

        public double UnitFrequency { get; private set; }
        public double GridFrequency { get; private set; }
        // 0 at 12 o'clock, positive clockwise (the unit running fast), −180 … 180.
        public double PhaseDegrees { get; private set; }

        public double SlipHz => UnitFrequency - GridFrequency;

        // Starts the unit running up a little off the grid's frequency, at a random phase.
        public Synchroscope(double gridFrequency, SeededRandom rng)
        {
            GridFrequency = gridFrequency;
            double off = 0.15 + rng.NextDouble() * 0.35;
            UnitFrequency = gridFrequency + (rng.NextDouble() < 0.5 ? -off : off);
            PhaseDegrees = rng.NextDouble() * 360 - 180;
        }

        // Real seconds: the needle turns once a second per hertz of slip.
        public void Step(double gridFrequency, double dt)
        {
            GridFrequency = gridFrequency;
            PhaseDegrees = Wrap(PhaseDegrees + 360 * SlipHz * dt);
        }

        public void Faster() => UnitFrequency += NudgeHz;
        public void Slower() => UnitFrequency -= NudgeHz;

        public Close Judge()
        {
            double phase = Math.Abs(PhaseDegrees), slip = Math.Abs(SlipHz);
            if (phase <= CleanPhaseDegrees && slip <= CleanSlipHz) return Close.Clean;
            if (phase <= RoughPhaseDegrees && slip <= RoughSlipHz) return Close.Rough;
            return Close.Slammed;
        }

        // The auto-synchroniser (research) closes on its own once slip and phase are clean.
        public bool ReadyToClose => Judge() == Close.Clean;

        private static double Wrap(double degrees)
        {
            degrees %= 360;
            if (degrees > 180) degrees -= 360;
            if (degrees < -180) degrees += 360;
            return degrees;
        }
    }
}
