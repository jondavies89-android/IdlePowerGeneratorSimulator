using System;
using NUnit.Framework;
using IdlePowerGen.Simulation;

namespace IdlePowerGen.Tests
{
    // The flywheel and water wheel on their own, with no scene: the sim must run headless.
    public class Era1FlywheelTests
    {
        private static Era1Flywheel Spun(double capacity, double spin)
        {
            var f = new Era1Flywheel(0.25);
            f.AddCapacity(capacity);
            f.AddSpin(spin);
            return f;
        }

        [Test]
        public void OneSecondDeliversTheExactDecayIntegral()
        {
            var f = Spun(10, 10);
            Assert.That(f.Tick(1.0), Is.EqualTo(10 * (1 - Math.Exp(-0.25)) / 0.25).Within(1e-9));
        }

        [Test]
        public void EnergyDoesNotDependOnStepSize()
        {
            var coarse = Spun(100, 100);
            var fine   = Spun(100, 100);
            double a = 0, b = 0;
            for (int i = 0; i < 10; i++)  a += coarse.Tick(1.0);
            for (int i = 0; i < 100; i++) b += fine.Tick(0.1) * 0.1;
            Assert.That(b, Is.EqualTo(a).Within(1e-6));
        }

        [Test]
        public void UntendedFlywheelComesToRest()
        {
            var f = Spun(100, 100);
            for (int i = 0; i < 120; i++) f.Tick(1.0);
            Assert.That(f.SpinWatts, Is.LessThan(1e-9));
        }

        [Test]
        public void SpinCannotExceedCapacity()
        {
            var f = Spun(5, 50);
            Assert.That(f.SpinWatts, Is.EqualTo(5.0));
        }

        [Test]
        public void ATurnMidTickStartsDecayingFromWhenItWasMade()
        {
            var f = new Era1Flywheel(0.25);
            f.AddCapacity(10);
            f.AddSpin(10, secondsIntoTick: 0.5);
            Assert.That(f.Tick(1.0), Is.EqualTo(10 * (1 - Math.Exp(-0.125)) / 0.25).Within(1e-9));
        }

        [Test]
        public void PendingJoulesMatchWhatTheTickThenPays()
        {
            var a = Spun(10, 10);
            var b = Spun(10, 10);
            double pending = a.PendingJoules(1.0);
            Assert.That(b.Tick(1.0) * 1.0, Is.EqualTo(pending).Within(1e-9));
        }

        [Test]
        public void FlywheelSustainsNothing()
        {
            var f = Spun(100, 100);
            Assert.That(f.SustainedOutputWatts, Is.EqualTo(0.0));
        }

        [Test]
        public void WaterWheelDeliversItsWattsTimesTheMagnets()
        {
            var w = new Era1WaterWheel();
            w.AddWatts(25);
            w.MultiplyOutput(4);
            Assert.That(w.Tick(1.0), Is.EqualTo(100.0));
        }

        [Test]
        public void WaterWheelIsSustained()
        {
            var w = new Era1WaterWheel();
            w.AddWatts(25);
            Assert.That(w.SustainedOutputWatts, Is.EqualTo(25.0));
        }
    }
}
