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
