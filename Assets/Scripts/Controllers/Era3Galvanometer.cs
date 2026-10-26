using System;
using IdlePowerGen.Data;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // The bench galvanometer (Era 3's Research tab). The needle swings across the scale; a
    // reading taken while it is inside the marked band earns research, more for each good
    // reading in a row, and the band moves. A miss ends the run, and the needle takes a
    // moment to settle. Real time, and only while the player is here.
    public sealed class Era3Galvanometer
    {
        public readonly struct Reading
        {
            public readonly bool   Hit;
            public readonly double Research;
            public Reading(bool hit, double research) { Hit = hit; Research = research; }
        }

        private readonly SeededRandom _rng;
        private double _phase;

        public Era3Galvanometer(SeededRandom rng)
        {
            _rng = rng;
            MoveBand();
        }

        // −1 (left stop) … +1 (right stop).
        public double Needle => Math.Sin(_phase);
        public double BandCentre { get; private set; }
        public double BandHalfWidth =>
            Math.Max(Era3Data.MinBandHalfWidth, Era3Data.BandHalfWidth - Era3Data.BandNarrowsPerRun * Run);

        public int    Run           { get; private set; } // good readings in a row
        public int    Readings      { get; private set; } // good readings this session
        public double Earned        { get; private set; } // research points this session
        public float  SettleSeconds { get; private set; }
        public bool   Ready         => SettleSeconds <= 0;

        public bool InBand => Math.Abs(Needle - BandCentre) <= BandHalfWidth;

        public void Tick(float realSeconds)
        {
            SettleSeconds = Math.Max(0, SettleSeconds - realSeconds);
            double speed = Era3Data.NeedleRadiansPerSecond * (1 + Era3Data.NeedleSpeedUpPerRun * Math.Min(Run, 8));
            _phase = (_phase + speed * realSeconds) % (2 * Math.PI);
        }

        // What the next good reading would be worth, given the works' research per game hour.
        public double Worth(double researchPerHour) =>
            Math.Max(Era3Data.ReadingMinResearch, researchPerHour * Era3Data.ReadingShareOfHour) *
            (1 + Math.Min(Era3Data.MaxRunBonus, Era3Data.RunBonusPerReading * Run));

        public Reading Take(double researchPerHour)
        {
            if (!Ready) return new Reading(false, 0);
            if (!InBand)
            {
                Run = 0;
                SettleSeconds = Era3Data.MissSettleSeconds;
                return new Reading(false, 0);
            }
            double points = Worth(researchPerHour);
            Run++;
            Readings++;
            Earned += points;
            SettleSeconds = Era3Data.HitSettleSeconds;
            MoveBand();
            return new Reading(true, points);
        }

        // Somewhere the needle passes, never hard against a stop.
        private void MoveBand() => BandCentre = (_rng.NextDouble() * 2 - 1) * 0.75;

        // Tests: the needle put in (or out of) the band.
        public void AimForTests(bool intoBand) =>
            _phase = Math.Asin(Math.Clamp(intoBand ? BandCentre : BandCentre > 0 ? -0.95 : 0.95, -1, 1));
    }
}
