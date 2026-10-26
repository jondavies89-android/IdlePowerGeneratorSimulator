using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Models;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // The contract board of one era (Era 2's demonstration office, then every later era's
    // big clients): which contracts are this era's, how many can run at once, and booking.
    // The contract cards and lists work against this, whichever era owns them.
    public abstract class ContractDesk : MonoBehaviour
    {
        public Economy.ContractBoard Board { get; } = new();

        public abstract int EraNumber { get; }
        protected abstract int BaseSlots { get; }

        protected static Managers.GameManager GM => Managers.GameManager.Instance;

        public static double Now => GameClock.Instance != null ? GameClock.Instance.GameTimeSeconds : 0;

        public bool IsCurrentEra => Managers.EraManager.Instance != null && Managers.EraManager.Instance.Era == EraNumber;

        public IEnumerable<ContractDefinition> Contracts =>
            GM == null ? Enumerable.Empty<ContractDefinition>()
                       : GM.ContractCatalogue.Where(c => c != null && c.minEra == EraNumber);

        public IEnumerable<Economy.ContractInstance> Engagements =>
            GM == null ? Enumerable.Empty<Economy.ContractInstance>()
                       : GM.Contracts.ActiveContracts.Where(c => c.Definition.minEra == EraNumber);

        // Slots from the era's own upgrades only: Era 2's telegraph lines don't carry over.
        public int Slots
        {
            get
            {
                int slots = BaseSlots;
                var gm = GM;
                if (gm == null) return slots;
                foreach (var def in gm.UpgradeCatalogue)
                    if (def.effect == UpgradeEffect.AddContractSlot && Managers.UpgradeEffectApplier.Applies(def))
                        slots += (int)System.Math.Round(def.effectValue) * gm.Upgrades.Level(def.upgradeId);
                return slots;
            }
        }

        public bool HasFreeSlot => GM != null && Engagements.Count() < Slots;

        // Watts the works has promised to its contract clients right now.
        public double CommittedWatts => Engagements.Sum(c => c.Definition.requiredWatts);

        // What the works could give a contract client: Era 2's whole output; from Era 3 what
        // is left after the connected customers, at the customers' end of the lines.
        public abstract double SpareForContracts { get; }

        public bool IsOffered(ContractDefinition def) =>
            IsCurrentEra && GM != null && Board.IsOffered(def, GM.Ledger.Reputation, GM.Contracts.ActiveContracts);

        public bool Book(ContractDefinition def) => Book(def, 0);

        private bool Book(ContractDefinition def, int managerNumber) =>
            IsCurrentEra && GM != null && HasFreeSlot &&
            Board.TryBook(def, Now, GM.Ledger.Reputation, GM.Contracts, GM.Contracts.ActiveContracts.Count + 1,
                          managerNumber, managerNumber > 0 ? ManagerCommission : 0f);

        // ---- The era's capstone ----
        // Once the era's goal is met the player closes the era when they choose: a panel says
        // so the moment it happens, and a banner above the tabs waits until they go.

        public bool CapstoneReady { get; protected set; }

        protected void OfferCapstone()
        {
            if (CapstoneReady || !IsCurrentEra) return;
            CapstoneReady = true;
            EventBus.Publish(new CapstoneReadyEvent(EraNumber));
        }

        public void SetCapstoneFromSave(bool ready) => CapstoneReady = ready;

        // DEV strip: the era's goal met at once, to test the way out.
        public void MeetGoalForDev() => OfferCapstone();

        // Closes the era. Past the last era built: the end-of-content card, and the era runs on.
        public void TakeCapstone()
        {
            if (!CapstoneReady || !IsCurrentEra) return;
            int next = EraNumber + 1;
            if (next > Managers.EraManager.LastBuiltEra) { EventBus.Publish(new ContentEndEvent(EraNumber)); return; }
            Managers.EraManager.Instance?.AdvanceTo(next);
        }

        // ---- Contract managers ----
        // One per contract slot, hired a level at a time from the era's tree. Each books its
        // chosen client (or the best-paying one it can) whenever its slot is free, and only
        // when the works can supply the whole of it, so it never books a contract to fail.

        // The share of every payment a manager keeps. Contracts the player starts pay in full.
        public const float ManagerCommission = 0.10f;

        private readonly List<Economy.ContractManager> _managers = new();

        // Managers hired in this era (the era's own upgrades, like its slots).
        public int ManagersHired
        {
            get
            {
                var gm = GM;
                if (gm == null) return 0;
                int n = 0;
                foreach (var def in gm.UpgradeCatalogue)
                    if (def.effect == UpgradeEffect.AddContractManager && def.minEra == EraNumber)
                        n += gm.Upgrades.Level(def.upgradeId);
                return n;
            }
        }

        public Economy.ContractManager Manager(int index)
        {
            while (_managers.Count <= index) _managers.Add(new Economy.ContractManager());
            return _managers[index];
        }

        // The clients a manager can be set to: every one the repute has opened.
        public List<ContractDefinition> ManagerChoices =>
            GM == null ? new List<ContractDefinition>()
                       : Contracts.Where(c => GM.Ledger.Reputation >= c.requiredReputation)
                                  .OrderBy(c => c.requiredReputation).ThenBy(c => c.requiredWatts).ToList();

        // Steps a manager's choice through "best it can supply" and each client.
        public void CycleManager(int index, int step)
        {
            var m = Manager(index);
            var choices = ManagerChoices;
            int at = string.IsNullOrEmpty(m.ContractId) ? -1 : choices.FindIndex(c => c.contractId == m.ContractId);
            int count = choices.Count + 1; // the extra one is "best"
            int next = ((at + 1 + step) % count + count) % count - 1;
            m.ContractId = next < 0 ? null : choices[next].contractId;
        }

        public void ToggleManager(int index) => Manager(index).On = !Manager(index).On;

        // The contract the manager's own booking is running, if any.
        public Economy.ContractInstance ManagerRunning(int index) =>
            Engagements.FirstOrDefault(c => c.ManagerNumber == index + 1);

        // What the manager would book next: its chosen client, or the best-paying one the
        // works can fully supply that no other manager has chosen.
        public ContractDefinition ManagerTarget(int index)
        {
            var m = Manager(index);
            if (!string.IsNullOrEmpty(m.ContractId))
                return Contracts.FirstOrDefault(c => c.contractId == m.ContractId);
            var chosen = new HashSet<string>();
            for (int i = 0; i < ManagersHired; i++)
                if (i != index && !string.IsNullOrEmpty(Manager(i).ContractId)) chosen.Add(Manager(i).ContractId);
            double spare = SpareForContracts;
            return Contracts.Where(c => !chosen.Contains(c.contractId) && IsOffered(c) && spare >= c.requiredWatts)
                            .OrderByDescending(Pay).FirstOrDefault();
        }

        // Called every sim tick by the era's controller.
        protected void RunManagers()
        {
            if (!IsCurrentEra || GM == null) return;
            int hired = ManagersHired;
            for (int i = 0; i < hired; i++)
            {
                if (!HasFreeSlot) return;
                var m = Manager(i);
                if (!m.On || ManagerRunning(i) != null) continue;
                var def = ManagerTarget(i);
                if (def == null || !IsOffered(def) || SpareForContracts < def.requiredWatts) continue;
                Book(def, i + 1);
            }
        }

        // A manager switched off: the player starts its chosen client by hand, at full pay.
        public bool StartManually(int index)
        {
            var def = ManagerTarget(index);
            return def != null && Book(def);
        }

        // ---- Save ----

        public void WriteManagers(Save.SaveData data)
        {
            for (int i = 0; i < ManagersHired; i++)
                data.contractManagers.Add(new Save.SaveData.ContractManagerEntry
                    { on = Manager(i).On, contractId = Manager(i).ContractId ?? "" });
        }

        public void ReadManagers(Save.SaveData data)
        {
            _managers.Clear();
            if (data?.contractManagers == null) return;
            foreach (var e in data.contractManagers)
                _managers.Add(new Economy.ContractManager
                    { On = e.on, ContractId = string.IsNullOrEmpty(e.contractId) ? null : e.contractId });
        }

        // The grid has gone dark: every manager is switched off, so none books a client into
        // the restoration. The player switches them back on when the lights are back.
        protected void StopManagers()
        {
            for (int i = 0; i < ManagersHired; i++) Manager(i).On = false;
        }

        // A new era's desk starts with its managers on and choosing for themselves.
        protected void ResetManagers() => _managers.Clear();

        // The whole fee, premium included, in pennies.
        public static decimal Pay(ContractDefinition def) =>
            GM == null ? 0m
                       : (decimal)(def.durationHours * def.requiredWatts * GM.ContractBaseRate(def) * def.payRateMultiplier);
    }
}
