namespace IdlePowerGen.Simulation
{
    // A place that draws power and pays for what it gets: an Era 3 district, an Era 4
    // feeder, an Era 5 city. Its demand follows the day (bible §3.4) around BaseWatts.
    public class LoadArea
    {
        public string Id         { get; }
        public double BaseWatts  { get; }
        // Blocks (Era 3) or line-lengths (Era 5) from the station, for losses.
        public double Distance   { get; }

        public bool Connected { get; set; }
        // Switched off by hand to save the grid (Era 4–5), or by the frequency relays.
        public bool Shed      { get; set; }

        // Loss fraction per watt delivered: delivering x costs x + LossPerWatt·x² at the
        // station (the I²R shape). Set by the era from the mains, the copper and the voltage.
        public double LossPerWatt { get; set; }

        // Reactive power drawn per watt of real power (Era 5): tan φ of the load's power factor.
        public double ReactivePerWatt { get; set; }

        // ---- Results of the last share-out ----
        public double DemandWatts    { get; set; }
        public double DeliveredWatts { get; set; }
        public double LossWatts      { get; set; }

        public bool   Supplied => Connected && !Shed;
        public double SentWatts  => DeliveredWatts + LossWatts;
        // Share of what was sent that the line ate.
        public double LossFraction => SentWatts > 0 ? LossWatts / SentWatts : 0;
        // Share of the demand that arrived (below 1: the lamps are dim).
        public double ServedFraction => DemandWatts > 0 ? DeliveredWatts / DemandWatts : 1;

        public LoadArea(string id, double baseWatts, double distance)
        {
            Id = id;
            BaseWatts = baseWatts;
            Distance = distance;
        }
    }
}
