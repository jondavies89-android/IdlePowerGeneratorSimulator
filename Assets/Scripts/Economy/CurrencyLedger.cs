using System;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Holds all three Era 1–2 currencies.
    // §6.4: double for joules (energy), decimal for money display.
    public class CurrencyLedger
    {
        public double  Joules     { get; private set; }
        public decimal Pennies    { get; private set; }
        public float   Reputation { get; private set; }

        // --- Joules ---

        public void AddJoules(double amount)
        {
            Joules += amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Joules, Joules));
        }

        public bool SpendJoules(double amount)
        {
            if (Joules < amount) return false;
            Joules -= amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Joules, Joules));
            return true;
        }

        // --- Pennies ---

        public void AddPennies(decimal amount)
        {
            Pennies += amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Pennies, (double)Pennies));
        }

        public bool SpendPennies(decimal amount)
        {
            if (Pennies < amount) return false;
            Pennies -= amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Pennies, (double)Pennies));
            return true;
        }

        // --- Reputation ---

        public void AddReputation(float delta)
        {
            Reputation = Math.Max(0f, Reputation + delta);
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Reputation, Reputation));
        }

        // --- Save / Load ---

        public void SetFromSave(double joules, decimal pennies, float reputation)
        {
            Joules     = joules;
            Pennies    = pennies;
            Reputation = reputation;
        }
    }
}
