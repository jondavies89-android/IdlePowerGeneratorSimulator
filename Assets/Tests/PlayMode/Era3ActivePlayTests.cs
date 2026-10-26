using NUnit.Framework;
using IdlePowerGen.Controllers;
using IdlePowerGen.Utils;
using static IdlePowerGen.Data.Era3Data;

namespace IdlePowerGen.Tests
{
    // Era 3's active play on its own, with no scene: the boilers and the galvanometer.
    public class Era3ActivePlayTests
    {
        // --- The boilers ---

        [Test]
        public void AStokeRaisesThePressure()
        {
            var b = new Era3Boiler();
            b.Stoke();
            Assert.That(b.Pressure, Is.EqualTo(StokePerTap));
        }

        [Test]
        public void ThePressureFallsOnItsOwn()
        {
            var b = new Era3Boiler();
            b.Stoke(); b.Stoke(); b.Stoke();
            b.Tick(1f);
            Assert.That(b.Pressure, Is.EqualTo(3 * StokePerTap - PressureFallPerSecond).Within(1e-5));
        }

        private static Era3Boiler OverStoked()
        {
            var b = new Era3Boiler();
            while (!b.ValveLifted) b.Stoke();
            return b;
        }

        [Test]
        public void StokingPastTheTopLiftsTheValve() =>
            Assert.That(OverStoked().Pressure, Is.EqualTo(ValveResetPressure));

        [Test]
        public void ALiftedValveCantBeStoked() =>
            Assert.That(OverStoked().Stoke(), Is.False);

        [Test]
        public void TheValveSettlesAfterAFewSeconds()
        {
            var b = OverStoked();
            b.Tick(ValveLiftSeconds + 0.1f);
            Assert.That(b.Stoke(), Is.True);
        }

        [Test]
        public void TheGreenShareCountsTimeInTheGreen()
        {
            var b = new Era3Boiler();
            while (b.Pressure < GreenLow + 0.1f) b.Stoke();
            b.Tick(1f);
            Assert.That(b.TakeGreenShare(), Is.EqualTo(1).Within(1e-9));
        }

        // --- The galvanometer ---

        private static Era3Galvanometer Galvanometer() => new(new SeededRandom(seed: 7));

        [Test]
        public void AReadingInTheBandEarnsResearch()
        {
            var g = Galvanometer();
            g.AimForTests(intoBand: true);
            Assert.That(g.Take(0).Research, Is.EqualTo(ReadingMinResearch));
        }

        [Test]
        public void AReadingOutsideTheBandEarnsNothing()
        {
            var g = Galvanometer();
            g.AimForTests(intoBand: false);
            Assert.That(g.Take(0).Hit, Is.False);
        }

        [Test]
        public void ARunOfReadingsIsWorthMore()
        {
            var g = Galvanometer();
            g.AimForTests(intoBand: true);
            g.Take(0);
            g.Tick(HitSettleSeconds + 0.01f);
            g.AimForTests(intoBand: true);
            Assert.That(g.Take(0).Research, Is.EqualTo(ReadingMinResearch * (1 + RunBonusPerReading)).Within(1e-9));
        }

        [Test]
        public void AMissEndsTheRun()
        {
            var g = Galvanometer();
            g.AimForTests(intoBand: true);
            g.Take(0);
            g.Tick(HitSettleSeconds + 0.01f);
            g.AimForTests(intoBand: false);
            g.Take(0);
            Assert.That(g.Run, Is.Zero);
        }

        [Test]
        public void TheNeedleMustSettleBeforeTheNextReading()
        {
            var g = Galvanometer();
            g.AimForTests(intoBand: true);
            g.Take(0);
            g.AimForTests(intoBand: true);
            Assert.That(g.Take(0).Hit, Is.False);
        }

        [Test]
        public void AReadingIsWorthAShareOfAnHoursResearch() =>
            Assert.That(Galvanometer().Worth(200), Is.EqualTo(200 * ReadingShareOfHour).Within(1e-9));
    }
}
