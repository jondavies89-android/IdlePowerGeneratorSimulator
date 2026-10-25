using System;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Economy
{
    // Holds every currency: Era 1's joules, the money (pennies in Era 2, shown as pounds from
    // Era 3), reputation, and from Era 3 research points.
    // §6.4: double for joules (energy), decimal for money display.
    public class CurrencyLedger
    {
        public double  Joules         { get; private set; }
        public decimal Pennies        { get; private set; }
        public float   Reputation     { get; private set; }
        public double  ResearchPoints { get; private set; }

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

        // Takes what there is, down to nothing: running costs that can't be refused.
        public decimal DrainPennies(decimal amount)
        {
            decimal taken = Math.Min(Pennies, Math.Max(0m, amount));
            if (taken <= 0) return 0m;
            Pennies -= taken;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Pennies, (double)Pennies));
            return taken;
        }

        // --- Reputation ---

        public void AddReputation(float delta)
        {
            Reputation = Math.Max(0f, Reputation + delta);
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Reputation, Reputation));
        }

        // --- Research points ---

        public void AddResearch(double amount)
        {
            if (amount <= 0) return;
            ResearchPoints += amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Research, ResearchPoints));
        }

        public bool SpendResearch(double amount)
        {
            if (ResearchPoints < amount) return false;
            ResearchPoints -= amount;
            EventBus.Publish(new CurrencyChangedEvent(CurrencyType.Research, ResearchPoints));
            return true;
        }

        // --- Save / Load ---

        public void SetFromSave(double joules, decimal pennies, float reputation, double research = 0)
        {
            Joules         = joules;
            Pennies        = pennies;
            Reputation     = reputation;
            ResearchPoints = research;
        }
    }
}
