using NUnit.Framework;
using IdlePowerGen.Simulation;

namespace IdlePowerGen.Tests
{
    // The share-out of power among load areas and the line losses (Eras 3–5), headless.
    public class TransmissionNetworkTests
    {
        private static LoadArea Area(double demand, double lossPerWatt = 0, double reactive = 0, bool connected = true)
        {
            var a = new LoadArea("a", demand, 1) { Connected = connected, LossPerWatt = lossPerWatt, ReactivePerWatt = reactive };
            a.DemandWatts = demand;
            return a;
        }

        [Test]
        public void WithPowerToSpareEveryAreaGetsWhatItAsks()
        {
            var a = Area(100e3);
            TransmissionNetwork.Share(new[] { a, Area(50e3) }, 1e6);
            Assert.That(a.DeliveredWatts, Is.EqualTo(100e3).Within(1e-6));
        }

        [Test]
        public void TheLineLosesInProportionToThePowerItCarries()
        {
            // 1 MW at 1e-7 per watt: a tenth of what is delivered is lost on the way.
            var a = Area(1e6, 1e-7);
            TransmissionNetwork.Share(new[] { a }, 10e6);
            Assert.That(a.LossWatts, Is.EqualTo(0.1 * 1e6).Within(1e-3));
        }

        [Test]
        public void TwiceThePowerLosesFourTimesAsMuch()
        {
            var small = Area(1e6, 1e-8);
            var big = Area(2e6, 1e-8);
            TransmissionNetwork.Share(new[] { small }, 1e9);
            TransmissionNetwork.Share(new[] { big }, 1e9);
            Assert.That(big.LossWatts / small.LossWatts, Is.EqualTo(4).Within(1e-9));
        }

        [Test]
        public void ShortOfPowerEveryAreaIsDimmedAlike()
        {
            var near = Area(100e3, 1e-7);
            var far = Area(300e3, 5e-7);
            TransmissionNetwork.Share(new[] { near, far }, 200e3);
            Assert.That(near.ServedFraction, Is.EqualTo(far.ServedFraction).Within(1e-9));
        }

        [Test]
        public void ShortOfPowerEverythingAvailableIsSent()
        {
            var near = Area(100e3, 1e-7);
            var far = Area(300e3, 5e-7);
            TransmissionNetwork.Share(new[] { near, far }, 200e3);
            Assert.That(near.SentWatts + far.SentWatts, Is.EqualTo(200e3).Within(1e-3));
        }

        [Test]
        public void ReactivePowerAddsToTheLosses()
        {
            // q = 0.5: the current is √1.25 as big for the same real power, so 1.25× the loss.
            var plain = Area(1e6, 1e-8);
            var mills = Area(1e6, 1e-8, reactive: 0.5);
            TransmissionNetwork.Share(new[] { plain }, 1e9);
            TransmissionNetwork.Share(new[] { mills }, 1e9);
            Assert.That(mills.LossWatts / plain.LossWatts, Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void AnAreaNotConnectedGetsNothing()
        {
            var off = Area(100e3, connected: false);
            TransmissionNetwork.Share(new[] { off }, 1e6);
            Assert.That(off.DeliveredWatts, Is.EqualTo(0));
        }

        [Test]
        public void AShedAreaGetsNothing()
        {
            var shed = Area(100e3);
            shed.Shed = true;
            TransmissionNetwork.Share(new[] { shed }, 1e6);
            Assert.That(shed.DeliveredWatts, Is.EqualTo(0));
        }
    }
}
