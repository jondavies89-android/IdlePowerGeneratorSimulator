using System;
using IdlePowerGen.Data;

namespace IdlePowerGen.Controllers
{
    // Era 3's boiler house, stoked by hand (the Town page). Each stoke raises the steam
    // pressure, which falls away on its own; held in the green band the works earns twice as
    // much. Stoke past the top and the safety valve lifts: the pressure drops back and
    // the firemen stand clear for a few seconds. Real time, and only while the player is here.
    public sealed class Era3Boiler
    {
        public float Pressure      { get; private set; } // 0 cold … 1 the valve lifts
        public float LockedSeconds { get; private set; } // left until it can be stoked again
        public int   ValveLifts    { get; private set; }

        public bool InGreen     => Pressure >= Era3Data.GreenLow && Pressure <= Era3Data.GreenHigh;
        public bool ValveLifted => LockedSeconds > 0;

        private double _green, _elapsed;

        // One shovelful. False while the valve is lifted.
        public bool Stoke()
        {
            if (ValveLifted) return false;
            Pressure += Era3Data.StokePerTap;
            if (Pressure >= 1f)
            {
                Pressure = Era3Data.ValveResetPressure;
                LockedSeconds = Era3Data.ValveLiftSeconds;
                ValveLifts++;
            }
            return true;
        }

        public void Tick(float realSeconds)
        {
            LockedSeconds = Math.Max(0, LockedSeconds - realSeconds);
            Pressure = Math.Max(0, Pressure - Era3Data.PressureFallPerSecond * realSeconds);
            _elapsed += realSeconds;
            if (InGreen) _green += realSeconds;
        }

        // The share of the time since it was last asked that the steam was in the green: what
        // the sim tick pays the bonus on. Asked twice within a frame (a sped-up clock), the
        // second answer is the pressure as it stands.
        public double TakeGreenShare()
        {
            double share = _elapsed > 0 ? _green / _elapsed : InGreen ? 1 : 0;
            _green = _elapsed = 0;
            return share;
        }

        public void Reset()
        {
            Pressure = LockedSeconds = 0;
            _green = _elapsed = 0;
        }
    }
}
