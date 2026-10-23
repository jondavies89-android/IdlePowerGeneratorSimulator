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

        public double OutputWatts =>
            Simulation.SimulationTick.Instance != null
                ? Simulation.SimulationTick.Instance.Grid.ComputeOutputWatts() : 0;

        public bool CanAccept => Current == Stage.Offered && OutputWatts >= Contract.requiredWatts;

        // Fraction of the contract hour that has passed.
        public float Progress
        {
            get
            {
                var c = ActiveContract;
                var clock = Utils.GameClock.Instance;
                if (c == null || clock == null) return 0f;
                return Mathf.Clamp01((float)((clock.GameTimeSeconds - c.StartGameTime)
                                             / (c.EndGameTime - c.StartGameTime)));
            }
        }

        public bool Accept()
        {
            if (!CanAccept) return false;
            double now = Utils.GameClock.Instance != null ? Utils.GameClock.Instance.GameTimeSeconds : 0;
            return Managers.GameManager.Instance.Contracts.TryAccept(Contract, now, maxConcurrent: 1);
        }
    }
}
