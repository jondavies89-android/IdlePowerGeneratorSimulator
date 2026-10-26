namespace IdlePowerGen.Economy
{
    // A contract manager hired for one contract slot: books its client whenever the slot is
    // free and the works can supply them, and keeps a commission on what that client pays.
    // Switched off, it books nothing, and the player starts the chosen client by hand.
    public sealed class ContractManager
    {
        public bool On = true;

        // The client to book, or null for the best-paying one the works can fully supply.
        public string ContractId;
    }
}
