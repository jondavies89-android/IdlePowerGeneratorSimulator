namespace IdlePowerGen.Managers
{
    // The purse is held in pennies in every era (decimal, so no rounding drift). Era 2 shows
    // it in old pence; from Era 3 it shows as pounds sterling, 240 d to the pound, so the
    // Era 2 purse carries into Era 3 at the historical rate with nothing converted.
    public static class Money
    {
        public const decimal PenniesPerPound = 240m;
        public const int     FirstPoundsEra  = 3;

        public static decimal FromPounds(double pounds) => (decimal)pounds * PenniesPerPound;
        public static double  ToPounds(decimal pennies) => (double)(pennies / PenniesPerPound);

        public static string Format(decimal pennies) => Format(pennies, CurrentEra);

        public static string Format(decimal pennies, int era) =>
            era >= FirstPoundsEra ? Utils.NumberFormatter.FormatPounds(pennies / PenniesPerPound)
                                  : Utils.NumberFormatter.FormatPennies(pennies);

        private static int CurrentEra => EraManager.Instance != null ? EraManager.Instance.Era : 1;
    }
}
