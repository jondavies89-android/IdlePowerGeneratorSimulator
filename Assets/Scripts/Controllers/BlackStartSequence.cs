using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // The second unit's start sequence (bible §10.3.4), done by hand, one step at a time:
    // close the auxiliary breakers supply-first, run the lube oil pump up to pressure, and
    // warm the turbine with steam no faster than its casing will take. Nothing here waits on
    // a clock; each step is as quick as the player's hands. (Whether a finished step takes
    // is still down to preparedness: BlackStartSession rolls for it.)
    public class BlackStartSequence
    {
        public enum Kind { Breakers, OilPump, Warming }

        // ---- The auxiliary breakers ----
        // Supply before load: the transformer feeds the board, the board feeds the pumps.
        public static readonly string[] BreakerOrder = { "Station transformer", "Unit board", "Boiler feed pumps" };
        public IReadOnlyList<string> BreakersShown { get; }
        public int BreakersClosed { get; private set; }

        // ---- The lube oil ----
        public const double PressureRisePerSecond = 0.35, PressureFallPerSecond = 0.20;
        public const double OilLow = 0.60, OilHigh = 0.85;
        // Past full pressure the relief valve lifts and dumps most of it.
        public const double ReliefAt = 1.0, PressureAfterRelief = 0.40;
        public const double OilSeconds = 3.0;
        public double Pressure { get; private set; }
        public double OilTime { get; private set; }
        public bool PumpHeld { get; set; }

        // ---- Warming the turbine ----
        // Half open warms it in under seven seconds and never stresses it; wider is quicker
        // and stresses the casing, so pushing it is a gamble against the stress gauge.
        public const double WarmPerSecond = 0.30;
        public const double SafeOpening = 0.5;
        public const double StressPerSecond = 1.5, StressReliefPerSecond = 0.5;
        public double Valve { get; private set; }
        public double Temperature { get; private set; }
        public double Stress { get; private set; }

        public string Message { get; private set; }
        // The step went wrong by the player's hand (a distorted casing): the session fails it.
        public bool Failed { get; private set; }

        public BlackStartSequence(SeededRandom rng)
        {
            // Shuffled, so the order is the player's reasoning and not the list's.
            var shown = BreakerOrder.ToList();
            for (int i = shown.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (shown[i], shown[j]) = (shown[j], shown[i]);
            }
            if (shown.SequenceEqual(BreakerOrder)) (shown[0], shown[2]) = (shown[2], shown[0]);
            BreakersShown = shown;
        }

        public bool IsClosed(string breaker) => Array.IndexOf(BreakerOrder, breaker) < BreakersClosed;

        public bool CloseBreaker(string breaker)
        {
            if (IsClosed(breaker) || BreakersClosed >= BreakerOrder.Length) return false;
            if (breaker == BreakerOrder[BreakersClosed])
            {
                BreakersClosed++;
                Message = null;
                return true;
            }
            string behind = BreakerOrder[Array.IndexOf(BreakerOrder, breaker) - 1];
            BreakersClosed = 0;
            Message = $"The {breaker.ToLowerInvariant()} breaker tripped straight out: nothing behind it yet. " +
                      $"Everything opened. It needs the {behind.ToLowerInvariant()} first.";
            return false;
        }

        public void SetValve(double opening) => Valve = Math.Clamp(opening, 0, 1);

        public bool Done(Kind kind) => kind switch
        {
            Kind.Breakers => BreakersClosed >= BreakerOrder.Length,
            Kind.OilPump  => OilTime >= OilSeconds,
            _             => Temperature >= 1.0,
        };

        public void Tick(Kind kind, double dt)
        {
            if (Failed) return;
            switch (kind)
            {
                case Kind.OilPump:
                    Pressure = Math.Max(0, Pressure + (PumpHeld ? PressureRisePerSecond : -PressureFallPerSecond) * dt);
                    if (Pressure >= ReliefAt)
                    {
                        Pressure = PressureAfterRelief;
                        Message = "The relief valve lifted and dumped the pressure. Ease off before the top.";
                    }
                    if (Pressure >= OilLow && Pressure <= OilHigh) OilTime = Math.Min(OilSeconds, OilTime + dt);
                    break;
                case Kind.Warming:
                    Temperature = Math.Min(1.0, Temperature + Valve * WarmPerSecond * dt);
                    double push = Valve - SafeOpening;
                    Stress = Math.Max(0, Stress + (push > 0 ? push * StressPerSecond : -StressReliefPerSecond) * dt);
                    if (Stress >= 1.0)
                    {
                        Failed = true;
                        Message = "The casing heated unevenly and the rotor is rubbing. Warming it too fast.";
                    }
                    break;
            }
        }

        // Back to the start of a step, for a retry. A turbine keeps the warmth it had.
        public void Reset(Kind kind)
        {
            Failed = false;
            Message = null;
            switch (kind)
            {
                case Kind.Breakers: BreakersClosed = 0; break;
                case Kind.OilPump:  Pressure = 0; OilTime = 0; PumpHeld = false; break;
                case Kind.Warming:  Valve = 0; Stress = 0; break;
            }
        }
    }
}
