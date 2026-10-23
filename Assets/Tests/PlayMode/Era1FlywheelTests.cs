using System;
using NUnit.Framework;
using IdlePowerGen.Simulation;

namespace IdlePowerGen.Tests
{
    // The flywheel on its own, with no scene: the sim must run headless.
    public class Era1FlywheelTests
    {
        private static Era1Flywheel Spun(double capacity, double floor, double spin)
        {
            var f = new Era1Flywheel(0.25);
            f.AddCapacity(capacity);
            f.AddFloor(floor);
            f.AddSpin(spin);
            return f;
        }

        [Test]
        public void OneSecondDeliversTheExactDecayIntegral()
        {
            var f = Spun(10, 0, 10);
            Assert.That(f.Tick(1.0), Is.EqualTo(10 * (1 - Math.Exp(-0.25)) / 0.25).Within(1e-9));
        }

        [Test]
        public void EnergyDoesNotDependOnStepSize()
        {
            var coarse = Spun(100, 20, 100);
            var fine   = Spun(100, 20, 100);
            double a = 0, b = 0;
            for (int i = 0; i < 10; i++)  a += coarse.Tick(1.0);
            for (int i = 0; i < 100; i++) b += fine.Tick(0.1) * 0.1;
            Assert.That(b, Is.EqualTo(a).Within(1e-6));
        }

        [Test]
        public void SpinNeverDecaysBelowTheFloor()
        {
            var f = Spun(100, 20, 100);
            for (int i = 0; i < 60; i++) f.Tick(1.0);
            Assert.That(f.SpinWatts, Is.EqualTo(20.0).Within(1e-9));
        }

        [Test]
        public void FloorAboveCapacityStillHolds()
        {
            var f = Spun(5, 30, 0);
            Assert.That(f.Tick(1.0), Is.EqualTo(30.0).Within(1e-9));
        }

        [Test]
        public void SpinCannotExceedCapacity()
        {
            var f = Spun(5, 0, 50);
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
        public void SustainedOutputIsTheFloorTimesTheMagnets()
        {
            var f = Spun(100, 20, 100);
            f.MultiplyOutput(4);
            Assert.That(f.SustainedOutputWatts, Is.EqualTo(80.0));
        }
    }
}
