namespace IdlePowerGen.Save
{
    // A scene controller that keeps part of the save (an era's districts, units, stockpile).
    // SaveManager finds every one in the scene, so a new era's state needs no new code here.
    public interface ISaveParticipant
    {
        void WriteSave(SaveData data);

        // Called on every load and reset, after the era, upgrades and generators are restored
        // and before GameLoadedEvent. Must reset fully when the save holds nothing for it.
        void ReadSave(SaveData data);
    }
}
