using System;

namespace IdlePowerGen.Utils
{
    // §6.5 — 3 significant figures, largest applicable SI prefix.
    // Animating the prefix transition (W→kW etc.) is a UI concern, handled in Presentation layer.
    public static class NumberFormatter
    {
        private static readonly string[] Prefixes   = { "", "k", "M", "G", "T", "P", "E" };
        private static readonly double[] Thresholds = { 1e0, 1e3, 1e6, 1e9, 1e12, 1e15, 1e18 };

        public static string FormatWatts(double watts)   => Format(watts, "W");
        public static string FormatJoules(double joules) => Format(joules, "J");
        public static string FormatMoney(decimal amount) => $"£{amount:N2}";

        // Era 2's purse, in old pennies: "412 d", "8.5 d", then SI-style past ten thousand
        // ("12.5 kd" would read oddly, so the prefix sits on the number: "12.5k d").
        public static string FormatPennies(decimal amount)
        {
            if (amount < 0) return "-" + FormatPennies(-amount);
            if (amount < 10m) return $"{Math.Floor(amount * 10m) / 10m:0.#} d";
            if (amount < 10_000m) return $"{Math.Floor(amount):N0} d";
            string si = Format((double)amount, "");
            // Format gives "12.5 k"; close it up to "12.5k d".
            return si.Replace(" ", "") + " d";
        }

        private static string Format(double value, string unit)
        {
            if (value < 0) return "-" + Format(-value, unit);
            if (value == 0) return $"0 {unit}";

            int tier = 0;
            for (int i = Thresholds.Length - 1; i >= 0; i--)
            {
                if (value >= Thresholds[i]) { tier = i; break; }
            }

            double scaled = value / Thresholds[tier];
            string num = scaled >= 100 ? scaled.ToString("F0")
                       : scaled >= 10  ? scaled.ToString("F1")
                                       : scaled.ToString("F2");

            return $"{num} {Prefixes[tier]}{unit}";
        }
    }
}
