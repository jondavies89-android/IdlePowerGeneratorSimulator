using System;

namespace IdlePowerGen.Controllers
{
    // The old station's diesel set, cranked by hand (bible §10.3.2). Each turn of the crank
    // adds speed and the engine slows between turns, so it's a rhythm, not a count: keep the
    // speed in the green while compression builds, then fire it as the piston comes over the
    // top. Crank too hard and it kicks back; too slowly and it won't catch.
    public class BlackStartCrank
    {
        public enum Fire { NotReady, Caught, TooSlow, Missed }

        // About two turns a second hold the speed in the middle of the green (0.30 / 0.14).
        public const double SpeedPerTurn = 0.14;
        public const double FallPerSecond = 0.30;
        public const double GreenLow = 0.55, GreenHigh = 0.80;
        // A turn from the top of the green is safe; one past it kicks back.
        public const double KickBack = 0.95;
        public const double KickBackSeconds = 1.5;
        public const double KickBackSpeed = 0.15;
        // Seconds in the green to build compression, kept through a kick or a miss but for a
        // penalty, so a slip costs a little and never the whole start.
        public const double PrimeSeconds = 3.0;
        public const double KickBackPrimeLost = 1.0, MissPrimeLost = 1.5, SlowPrimeLost = 1.0;
        // The piston goes round about once a second in the green; the window either side of
        // the top is wide enough for a thumb's timing (about a fifth of a second).
        public const double RevsPerSecondAtFull = 1.6;
        public const double FireWindow = 0.12;

        public double Speed { get; private set; }
        public double Primed { get; private set; }
        public double Piston { get; private set; } // 0 (and 1) at the top
        public double Stunned { get; private set; }
        private string _message;
        public string Message { get => _message; private set { _message = value; _messageAge = 0; } }

        public bool Ready => Primed >= PrimeSeconds;
        public bool InGreen => Speed >= GreenLow && Speed <= GreenHigh;
        public bool AtTop => Math.Min(Piston, 1 - Piston) <= FireWindow;

        public void Turn()
        {
            if (Stunned > 0) return;
            Speed += SpeedPerTurn;
            if (Speed < KickBack) return;
            Speed = KickBackSpeed;
            Stunned = KickBackSeconds;
            Primed = Math.Max(0, Primed - KickBackPrimeLost);
            Message = "It kicked back! Steady: keep it in the green, not past it.";
        }

        // How long a slip's message stays up.
        public const double MessageSeconds = 3.0;
        private double _messageAge;

        public void Tick(double dt)
        {
            if (_message != null && (_messageAge += dt) > MessageSeconds) _message = null;
            Stunned = Math.Max(0, Stunned - dt);
            Speed = Math.Max(0, Speed - FallPerSecond * dt);
            Piston = (Piston + Speed * RevsPerSecondAtFull * dt) % 1.0;
            if (InGreen && !Ready) Primed = Math.Min(PrimeSeconds, Primed + dt);
        }

        public Fire TryFire()
        {
            if (!Ready) return Fire.NotReady;
            if (Speed < GreenLow)
            {
                Primed = Math.Max(0, Primed - SlowPrimeLost);
                Message = "Too slow to catch. Get it back into the green first.";
                return Fire.TooSlow;
            }
            if (!AtTop)
            {
                Primed = Math.Max(0, Primed - MissPrimeLost);
                Message = "It coughed and didn't catch. Fire as the piston comes over the top.";
                return Fire.Missed;
            }
            Message = null;
            return Fire.Caught;
        }
    }
}
