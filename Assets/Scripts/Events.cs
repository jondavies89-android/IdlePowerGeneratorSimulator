namespace IdlePowerGen
{
    // Runtime-layer events. Simulation and economy events live beside their assemblies
    // (Simulation/Events/SimulationEvents.cs, Economy/EconomyEvents.cs).

    // --- Interaction ---

    public readonly struct TapEvent
    {
        public readonly double WattsPerTap;
        public readonly double JoulesProduced;
        public TapEvent(double wpt, double j) { WattsPerTap = wpt; JoulesProduced = j; }
    }

    // --- Persistence ---

    // Raised after a save (or a reset) has been applied. Owners of derived runtime state
    // (upgrade effects, crank value) rebuild it from the purchased-upgrade set.
    public readonly struct GameLoadedEvent { }

    public readonly struct OfflineProgressEvent
    {
        public readonly double  SecondsAway;
        public readonly double  JoulesEarned;
        public readonly decimal PenniesEarned;
        public readonly string  FlavorText;
        public OfflineProgressEvent(double seconds, double joules, string flavor, decimal pennies = 0m)
        {
            SecondsAway = seconds; JoulesEarned = joules; FlavorText = flavor; PenniesEarned = pennies;
        }
    }

    // --- Era 2 works ---

    // Something at the works the player should hear about: coal ran out, the apprentices
    // downed tools. Plain English, ready for a toast.
    public readonly struct WorksNoticeEvent
    {
        public readonly string Message;
        public WorksNoticeEvent(string message) { Message = message; }
    }

    // --- Eras 2–5 ---

    // The era's goal is met (Era 2: the Exhibition lit; from Era 3 its output goal): its
    // capstone can be taken, and the era closed, whenever the player is ready.
    public readonly struct CapstoneReadyEvent
    {
        public readonly int Era;
        public CapstoneReadyEvent(int era) { Era = era; }
    }

    // The last built era's capstone was taken: there is no next era yet.
    public readonly struct ContentEndEvent
    {
        public readonly int Era;
        public ContentEndEvent(int era) { Era = era; }
    }

    // A preparedness score joins the dashboard (bible §14.1): the failure has happened, here is
    // its name, and here is the first thing to do about it.
    public readonly struct ScoreIntroducedEvent
    {
        public readonly string Score;
        public readonly string Title;
        public readonly string Story;
        public ScoreIntroducedEvent(string score, string title, string story) { Score = score; Title = title; Story = story; }
    }

    // The grid has gone dark: the black start begins (bible §10).
    public readonly struct BlackoutEvent
    {
        public readonly int Era;
        public BlackoutEvent(int era) { Era = era; }
    }

    // --- Milestones ---

    public readonly struct MilestoneReachedEvent
    {
        public readonly string Id;
        public readonly string Message;
        public MilestoneReachedEvent(string id, string message) { Id = id; Message = message; }
    }

    // --- Era ---

    public readonly struct EraAdvancedEvent
    {
        public readonly int FromEra;
        public readonly int ToEra;
        public EraAdvancedEvent(int from, int to) { FromEra = from; ToEra = to; }
    }
}
