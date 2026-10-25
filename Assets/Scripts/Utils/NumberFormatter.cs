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

        // From Era 3, pounds sterling as the bible's mockups show them: "£4,820", "£182,400",
        // then "£4.2M" from a million (trailing zeros trimmed, SI-style past that).
        public static string FormatPounds(decimal pounds)
        {
            if (pounds < 0) return "-" + FormatPounds(-pounds);
            if (pounds < 10m) return $"£{Math.Floor(pounds * 100m) / 100m:0.00}";
            if (pounds < 1_000_000m) return $"£{Math.Floor(pounds):N0}";
            string[] big = { "M", "B", "T", "Q" };
            double v = (double)pounds / 1e6;
            int tier = 0;
            while (v >= 1000 && tier < big.Length - 1) { v /= 1000; tier++; }
            string num = v >= 100 ? v.ToString("F0") : v >= 10 ? v.ToString("0.#") : v.ToString("0.##");
            return $"£{num}{big[tier]}";
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
            // Decide the decimals on the rounded value, so 99.996 kW reads "100 kW", not
            // "100.0 kW", and 999.6 kW moves up to "1.00 MW".
            if (scaled >= 999.5 && tier < Thresholds.Length - 1) { tier++; scaled = value / Thresholds[tier]; }
            string num = scaled >= 99.95 ? scaled.ToString("F0")
                       : scaled >= 9.995 ? scaled.ToString("F1")
                                         : scaled.ToString("F2");

            return $"{num} {Prefixes[tier]}{unit}";
        }
    }
}
