namespace IdlePowerGen.Save
{
    // A scene controller that earns while the player is away for longer than the short
    // catch-up (bible §7.1.2–7.1.3). Returns the pennies earned; SaveManager pays them.
    public interface IOfflineEarner
    {
        decimal OfflineEarnings(SaveData data, double realSeconds, float eraFactor, float tauHours);
    }
}
