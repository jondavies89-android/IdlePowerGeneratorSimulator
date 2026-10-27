using NUnit.Framework;
using IdlePowerGen.Controllers;
using IdlePowerGen.Utils;
using Kind = IdlePowerGen.Controllers.BlackStartSequence.Kind;

namespace IdlePowerGen.Tests
{
    // The black start's hands-on steps on their own, with no scene: the diesel's crank, a
    // hydro unit's gates, and the second unit's start sequence.
    public class BlackStartHandsOnTests
    {
        private const double Frame = 0.05;

        // Cranks the way a steady player does: a turn whenever the speed drops to the middle
        // of the green. Fires at the top once compression is up, if told to.
        public static void PlayCrank(BlackStartCrank c, double seconds, bool fire = false)
        {
            for (double t = 0; t < seconds; t += Frame)
            {
                if (c.Speed < 0.62) c.Turn();
                c.Tick(Frame);
                if (fire && c.Ready && c.AtTop && c.TryFire() == BlackStartCrank.Fire.Caught) return;
            }
        }

        // --- The crank ---

        [Test]
        public void CrankingTooHardKicksBack()
        {
            var c = new BlackStartCrank();
            for (int i = 0; i < 7; i++) c.Turn();
            Assert.That(c.Stunned, Is.GreaterThan(0));
        }

        [Test]
        public void SteadyCrankingBuildsCompression()
        {
            var c = new BlackStartCrank();
            PlayCrank(c, 5);
            Assert.That(c.Ready, Is.True);
        }

        [Test]
        public void FiringBeforeCompressionDoesNothing()
        {
            var c = new BlackStartCrank();
            PlayCrank(c, 1);
            Assert.That(c.TryFire(), Is.EqualTo(BlackStartCrank.Fire.NotReady));
        }

        [Test]
        public void FiringAwayFromTheTopMisses()
        {
            var c = new BlackStartCrank();
            PlayCrank(c, 5);
            while (c.AtTop) { if (c.Speed < 0.62) c.Turn(); c.Tick(0.01); }
            Assert.That(c.TryFire(), Is.EqualTo(BlackStartCrank.Fire.Missed));
        }

        [Test]
        public void FiringAtTheTopCatches()
        {
            var c = new BlackStartCrank();
            PlayCrank(c, 5);
            while (!c.AtTop) { if (c.Speed < 0.62) c.Turn(); c.Tick(0.01); }
            Assert.That(c.TryFire(), Is.EqualTo(BlackStartCrank.Fire.Caught));
        }

        [Test]
        public void FiringOnceTheCrankHasSlowedIsTooSlow()
        {
            var c = new BlackStartCrank();
            PlayCrank(c, 5);
            c.Tick(2); // no turns: it runs down
            Assert.That(c.TryFire(), Is.EqualTo(BlackStartCrank.Fire.TooSlow));
        }

        // --- The gates ---

        [Test]
        public void GatesLeftWideOpenTripOnOverspeed()
        {
            var g = new BlackStartGates();
            g.Set(1);
            bool tripped = false;
            for (double t = 0; t < 10 && !tripped; t += Frame) { g.Tick(Frame); tripped = g.Tripped > 0; }
            Assert.That(tripped, Is.True);
        }

        [Test]
        public void TrippedGatesWontOpen()
        {
            var g = new BlackStartGates();
            g.Set(1);
            for (double t = 0; t < 10 && g.Tripped <= 0; t += Frame) g.Tick(Frame);
            g.Set(1);
            Assert.That(g.Opening, Is.EqualTo(0));
        }

        [Test]
        public void EasingOffBeforeRatedSpeedHoldsTheTurbine()
        {
            var g = new BlackStartGates();
            for (double t = 0; t < 30 && !g.Running; t += Frame)
            {
                g.Set(g.Speed < 0.7 ? 1 : BlackStartGates.HoldOpening);
                g.Tick(Frame);
            }
            Assert.That(g.Running, Is.True);
        }

        // --- The start sequence ---

        private static BlackStartSequence Sequence() => new(new SeededRandom(7));

        [Test]
        public void TheBreakersAreShownOutOfOrder() =>
            Assert.That(Sequence().BreakersShown, Is.Not.EqualTo(BlackStartSequence.BreakerOrder));

        [Test]
        public void ClosingTheBreakersSupplyFirstCompletesTheStep()
        {
            var s = Sequence();
            foreach (var b in BlackStartSequence.BreakerOrder) s.CloseBreaker(b);
            Assert.That(s.Done(Kind.Breakers), Is.True);
        }

        [Test]
        public void ALoadBreakerBeforeItsSupplyOpensEverything()
        {
            var s = Sequence();
            s.CloseBreaker(BlackStartSequence.BreakerOrder[0]);
            s.CloseBreaker(BlackStartSequence.BreakerOrder[2]);
            Assert.That(s.BreakersClosed, Is.EqualTo(0));
        }

        [Test]
        public void OilHeldInTheBandCompletesTheStep()
        {
            var s = Sequence();
            for (double t = 0; t < 20 && !s.Done(Kind.OilPump); t += Frame)
            {
                s.PumpHeld = s.Pressure < 0.7;
                s.Tick(Kind.OilPump, Frame);
            }
            Assert.That(s.Done(Kind.OilPump), Is.True);
        }

        [Test]
        public void PumpingPastFullLiftsTheReliefValve()
        {
            var s = Sequence();
            s.PumpHeld = true;
            s.Tick(Kind.OilPump, 1.0 / BlackStartSequence.PressureRisePerSecond + 0.01);
            Assert.That(s.Pressure, Is.EqualTo(BlackStartSequence.PressureAfterRelief));
        }

        [Test]
        public void WarmingHalfOpenNeverStressesTheCasing()
        {
            var s = Sequence();
            s.SetValve(BlackStartSequence.SafeOpening);
            for (double t = 0; t < 10; t += Frame) s.Tick(Kind.Warming, Frame);
            Assert.That((s.Done(Kind.Warming), s.Failed), Is.EqualTo((true, false)));
        }

        [Test]
        public void WarmingWideOpenDistortsTheCasing()
        {
            var s = Sequence();
            s.SetValve(1);
            for (double t = 0; t < 2; t += Frame) s.Tick(Kind.Warming, Frame);
            Assert.That(s.Failed, Is.True);
        }

        [Test]
        public void ARetryKeepsTheWarmthTheTurbineHad()
        {
            var s = Sequence();
            s.SetValve(1);
            for (double t = 0; t < 2; t += Frame) s.Tick(Kind.Warming, Frame);
            double warmth = s.Temperature;
            s.Reset(Kind.Warming);
            Assert.That(s.Temperature, Is.EqualTo(warmth));
        }
    }
}
