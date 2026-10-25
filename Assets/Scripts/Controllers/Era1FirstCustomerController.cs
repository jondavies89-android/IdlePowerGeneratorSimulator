using System.Linq;
using UnityEngine;

namespace IdlePowerGen.Controllers
{
    // The last beat of Era 1 (bible §9.2): the neighbour who knocked at 500 J offers a
    // contract the workshop can only take once it makes the era's full 1 kW. Completing
    // it pays the first shilling, and EraManager advances to Era 2 on that payment.
    public class Era1FirstCustomerController : MonoBehaviour
    {
        public enum Stage { NotYet, Offered, Supplying, Done }

        public Models.ContractDefinition Contract =>
            Managers.GameManager.Instance?.FindContract(Data.Era1Data.FirstCustomerContractId);

        public Stage Current
        {
            get
            {
                var gm = Managers.GameManager.Instance;
                if (gm == null || Contract == null) return Stage.NotYet;
                if (Managers.EraManager.Instance != null && Managers.EraManager.Instance.CurrentEra >= 2f)
                    return Stage.Done;
                if (ActiveContract != null) return Stage.Supplying;
                var tracker = Managers.Era1MilestoneTracker.Instance;
                return tracker != null && tracker.HasReached(Data.Era1Data.FirstCustomerMilestoneId)
                    ? Stage.Offered : Stage.NotYet;
            }
        }

        public Economy.ContractInstance ActiveContract =>
            Managers.GameManager.Instance?.Contracts.ActiveContracts
                .FirstOrDefault(c => c.Definition.contractId == Data.Era1Data.FirstCustomerContractId);

        // The last tick's average, the same figure the contract is held to. The live reading
        // peaks just after each turn, so accepting on it could break the contract a second later.
        public double OutputWatts =>
            Simulation.SimulationTick.Instance != null
                ? Simulation.SimulationTick.Instance.Grid.TotalOutputWatts : 0;

        public bool CanAccept => Current == Stage.Offered && OutputWatts >= Contract.requiredWatts;

        // How close the workshop is to the wattage he needs, while it builds towards it.
        public float BuildProgress =>
            Contract == null || Contract.requiredWatts <= 0 ? 0f
                : Mathf.Clamp01((float)(OutputWatts / Contract.requiredWatts));

        // Set when output dipped and the contract broke off; cleared on the next handshake.
        public bool WasBroken { get; private set; }

        private void Awake()    => Utils.EventBus.Subscribe<ContractFailedEvent>(OnContractFailed);
        private void OnDestroy() => Utils.EventBus.Unsubscribe<ContractFailedEvent>(OnContractFailed);

        private void OnContractFailed(ContractFailedEvent evt)
        {
            if (evt.Contract.Definition.contractId == Data.Era1Data.FirstCustomerContractId)
                WasBroken = true;
        }

        // Fraction of the contract hour that has passed.
        public float Progress => ActiveContract?.Progress01 ?? 0f;

        public bool Accept()
        {
            if (!CanAccept) return false;
            double now = Utils.GameClock.Instance != null ? Utils.GameClock.Instance.GameTimeSeconds : 0;
            if (!Managers.GameManager.Instance.Contracts.TryAccept(Contract, now, maxConcurrent: 1)) return false;
            WasBroken = false;
            return true;
        }
    }
}
