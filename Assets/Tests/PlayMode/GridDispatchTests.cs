using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Tests
{
    // The grid's frequency (Eras 4–5) and the synchroscope, headless.
    public class GridDispatchTests
    {
        private static GridUnit Unit(double watts, float governor = 5f, int number = 1)
        {
            var def = ScriptableObject.CreateInstance<GeneratorDefinition>();
            def.basePowerWatts = watts;
            def.governorSeconds = governor;
            def.inertiaSeconds = 5;
            var u = new GridUnit(new GeneratorInstance(def), number);
            u.SetState(GridUnit.UnitState.Online);
            return u;
        }

        private static GridDispatch Run(List<GridUnit> units, double load, int seconds, GridDispatch grid = null)
        {
            grid ??= new GridDispatch(new SeededRandom(7));
            for (int i = 0; i < seconds; i++) grid.Step(units, null, load, 1);
            return grid;
        }

        [Test]
        public void WithReserveTheFrequencyHoldsAtFifty()
        {
            var grid = Run(new List<GridUnit> { Unit(100e6) }, 60e6, 180);
            Assert.That(grid.Frequency, Is.EqualTo(50).Within(0.05));
        }

        [Test]
        public void FivePercentShortTheFrequencySettlesAboutAHertzLow()
        {
            var grid = Run(new List<GridUnit> { Unit(100e6) }, 105e6, 240);
            Assert.That(grid.Frequency, Is.EqualTo(49.05).Within(0.15));
        }

        // A unit tripping is the surprise the governors have to answer: the needle dips.
        [Test]
        public void AUnitTrippingDipsTheFrequency()
        {
            var lost = Unit(40e6, number: 2);
            var units = new List<GridUnit> { Unit(100e6), lost };
            var grid = Run(units, 90e6, 120);
            lost.SetState(GridUnit.UnitState.Faulted, 1);
            double lowest = 50;
            for (int i = 0; i < 20; i++) { grid.Step(units, null, 90e6, 1); lowest = System.Math.Min(lowest, grid.Frequency); }
            Assert.That(lowest, Is.LessThan(49.8));
        }

        [Test]
        public void AfterATripWithReserveTheFrequencyComesBack()
        {
            var lost = Unit(40e6, number: 2);
            var units = new List<GridUnit> { Unit(100e6), lost };
            var grid = Run(units, 90e6, 120);
            lost.SetState(GridUnit.UnitState.Faulted, 1);
            Run(units, 90e6, 180, grid);
            Assert.That(grid.Frequency, Is.EqualTo(50).Within(0.05));
        }

        // Load the operators expect (an area switched in, the evening rise) the units follow.
        [Test]
        public void AScheduledRiseInLoadBarelyMovesTheNeedle()
        {
            var units = new List<GridUnit> { Unit(100e6) };
            var grid = Run(units, 60e6, 120);
            double lowest = 50;
            for (int i = 0; i < 30; i++) { grid.Step(units, null, 60e6 + i * 0.5e6, 1); lowest = System.Math.Min(lowest, grid.Frequency); }
            Assert.That(lowest, Is.GreaterThan(49.9));
        }

        [Test]
        public void UnitsShareTheLoadByTheirSize()
        {
            var big = Unit(100e6);
            var small = Unit(50e6, number: 2);
            Run(new List<GridUnit> { big, small }, 90e6, 180);
            Assert.That(big.OutputWatts / small.OutputWatts, Is.EqualTo(2).Within(0.02));
        }

        [Test]
        public void AUnitOffTheBarsSendsNothing()
        {
            var off = Unit(100e6);
            off.SetState(GridUnit.UnitState.AwaitingSync);
            Run(new List<GridUnit> { Unit(100e6, number: 2), off }, 50e6, 30);
            Assert.That(off.OutputWatts, Is.EqualTo(0));
        }

        [Test]
        public void ACollapsedGridIsDead()
        {
            var grid = Run(new List<GridUnit> { Unit(100e6) }, 50e6, 10);
            grid.Collapse();
            Assert.That(grid.Dead, Is.True);
        }

        // --- Synchroscope ---

        private static Synchroscope SlowScope()
        {
            var scope = new Synchroscope(50, new SeededRandom(11));
            while (System.Math.Abs(scope.SlipHz) > Synchroscope.NudgeHz / 2 + 1e-9)
                if (scope.SlipHz > 0) scope.Slower(); else scope.Faster();
            return scope;
        }

        [Test]
        public void TheNeedleTurnsAtTheSlip()
        {
            var scope = SlowScope();
            scope.Faster(); scope.Faster();
            double before = scope.PhaseDegrees;
            scope.Step(50, 0.1);
            double moved = Mathf.DeltaAngle((float)before, (float)scope.PhaseDegrees);
            Assert.That(moved, Is.EqualTo(36 * scope.SlipHz).Within(1e-3));
        }

        [Test]
        public void ClosingAtTwelveOClockWithLittleSlipIsClean()
        {
            var scope = SlowScope();
            if (System.Math.Abs(scope.SlipHz) < 1e-6) scope.Faster();
            for (int i = 0; i < 100000 && System.Math.Abs(scope.PhaseDegrees) > 5; i++) scope.Step(50, 0.01);
            Assert.That(scope.Judge(), Is.EqualTo(Synchroscope.Close.Clean));
        }

        [Test]
        public void ClosingHalfAWayRoundSlamsTheBreaker()
        {
            var scope = SlowScope();
            if (System.Math.Abs(scope.SlipHz) < 1e-6) scope.Faster();
            for (int i = 0; i < 100000 && System.Math.Abs(scope.PhaseDegrees) < 150; i++) scope.Step(50, 0.01);
            Assert.That(scope.Judge(), Is.EqualTo(Synchroscope.Close.Slammed));
        }
    }
}
