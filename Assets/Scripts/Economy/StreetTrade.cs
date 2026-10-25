using System;

namespace IdlePowerGen.Economy
{
    // Power no engagement is taking goes down the street main to shops and houses, who buy
    // by the hour at a lower price than a booked client (Era 2 onward). It keeps the purse
    // moving between engagements, and its price follows the day's demand, so the era's one
    // concept (demand isn't constant) is felt every hour, not only at booking time.
    public class StreetTrade
    {
        // What was sold on the last tick, for the stats line.
        public double  LastWatts          { get; private set; }
        public decimal LastPennies        { get; private set; }
        public double  LastPricePerWattHour { get; private set; }

        // pricePerWattHour: pennies per watt per game hour right now; 0 means no street trade.
        // gameSeconds: game time the tick covers. Returns the pennies paid.
        public decimal Sell(double spareWatts, double gameSeconds, double pricePerWattHour, CurrencyLedger ledger)
        {
            LastPricePerWattHour = Math.Max(0, pricePerWattHour);
            LastWatts = LastPricePerWattHour > 0 ? Math.Max(0, spareWatts) : 0;
            LastPennies = (decimal)(LastWatts * gameSeconds / 3600.0 * LastPricePerWattHour);
            if (LastPennies > 0) ledger.AddPennies(LastPennies);
            return LastPennies;
        }

        public void Reset()
        {
            LastWatts = 0;
            LastPennies = 0;
            LastPricePerWattHour = 0;
        }
    }
}
