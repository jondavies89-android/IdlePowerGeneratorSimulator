using System;
using System.Collections.Generic;

namespace IdlePowerGen.Simulation
{
    // Shares the power available at the station among the load areas it supplies, and works
    // out what the lines lose on the way (bible §2.2, Era 3: "distance has a cost").
    //
    // Delivering x watts to an area with loss coefficient k costs x + k·x² at the station:
    // current grows with the power and I²R with the square of the current. Reactive power
    // (Era 5) adds to the current without being delivered, so it scales k by 1 + q², q the
    // reactive watts per real watt. Short of power, every area is dimmed by the same factor
    // λ, found exactly from the quadratic Σ(λd + k'λ²d²) = available.
    public static class TransmissionNetwork
    {
        public readonly struct Result
        {
            public readonly double Demand;    // what the supplied areas asked for
            public readonly double Delivered; // what reached them
            public readonly double Losses;    // what the lines ate
            public readonly double Scale;     // λ: 1 when everyone was served in full
            public Result(double demand, double delivered, double losses, double scale)
            {
                Demand = demand; Delivered = delivered; Losses = losses; Scale = scale;
            }
            public double Sent => Delivered + Losses;
            public double LossFraction => Sent > 0 ? Losses / Sent : 0;
        }

        // Each area's DemandWatts must already be set for this moment. Areas not supplied
        // (unconnected or shed) get nothing and cost nothing.
        public static Result Share(IReadOnlyList<LoadArea> areas, double availableWatts)
        {
            double a = 0, b = 0;
            foreach (var area in areas)
            {
                if (!area.Supplied) { area.DeliveredWatts = area.LossWatts = 0; continue; }
                double d = Math.Max(0, area.DemandWatts);
                b += d;
                a += EffectiveLoss(area) * d * d;
            }

            double available = Math.Max(0, availableWatts);
            double scale;
            if (b <= 0) scale = 1;
            else if (b + a <= available) scale = 1;
            else if (a <= 1e-18) scale = available / b;
            // λ = (−b + √(b² + 4a·available)) / 2a, written to stay accurate when a is tiny.
            else scale = 2 * available / (b + Math.Sqrt(b * b + 4 * a * available));

            double delivered = 0, losses = 0;
            foreach (var area in areas)
            {
                if (!area.Supplied) continue;
                double x = Math.Max(0, area.DemandWatts) * scale;
                area.DeliveredWatts = x;
                area.LossWatts = EffectiveLoss(area) * x * x;
                delivered += x;
                losses += area.LossWatts;
            }
            return new Result(b, delivered, losses, scale);
        }

        public static double EffectiveLoss(LoadArea area) =>
            Math.Max(0, area.LossPerWatt) * (1 + area.ReactivePerWatt * area.ReactivePerWatt);
    }
}
