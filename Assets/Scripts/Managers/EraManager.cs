using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Managers
{
    // Tracks and advances the current era.
    // Era 1 → 2 transition fires when the player earns their first penny (first customer).
    // From Era 2 each era ends on its capstone, when the player takes it (ContractDesk), once
    // the era's goal is met: the Great Exhibition lit, then each era's output.
    public class EraManager : MonoBehaviour
    {
        public static EraManager Instance { get; private set; }

        // The last era with a screen; a capstone past it leaves the player in this era.
        public const int LastBuiltEra = 6;

        public float CurrentEra { get; private set; } = 1f;
        public int   Era => Mathf.FloorToInt(CurrentEra);

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            // In Awake, not Start: the save loads in GameManager.Start, which may run first.
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start()
        {
            EventBus.Subscribe<ContractCompletedEvent>(OnContractCompleted);
            ApplyEraToGrid();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContractCompletedEvent>(OnContractCompleted);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void OnGameLoaded(GameLoadedEvent _) => ApplyEraToGrid();

        private void OnContractCompleted(ContractCompletedEvent evt)
        {
            // First completed contract (which pays pennies) triggers Era 2.
            if (CurrentEra < 2f && evt.PenniesEarned > 0m)
                AdvanceTo(2);
        }

        // Public for the era shortcuts and the capstones; Era 1 reaches it through its contract.
        public void AdvanceTo(int era)
        {
            int from = (int)CurrentEra;
            CurrentEra = era;
            ApplyEraToGrid();
            // The old era's engagements end with it: they were paid as the power arrived, and
            // their clients have no place on the new era's board. (Era 1's one customer has
            // just finished when Era 2 begins.)
            if (era >= 3) GameManager.Instance?.Contracts.RemoveWhere(c => c.Definition.minEra < era);
            EventBus.Publish(new EraAdvancedEvent(from, era));
            Debug.Log($"[EraManager] Advanced to Era {era}");
        }

        public void SetEraFromSave(float era) => CurrentEra = era;

        // The previous era's works as one steady station at that era's full output (each
        // era's maxWatts, bible §2.1), whatever was actually built.
        public static double InheritedWattsFor(int era)
        {
            if (era < 2) return 0;
            var cfg = GameManager.Instance != null ? GameManager.Instance.GetEraConfig(era - 1) : null;
            return cfg != null ? cfg.maxWatts : 0;
        }

        private void ApplyEraToGrid()
        {
            var grid = Simulation.SimulationTick.Instance?.Grid;
            if (grid == null) return;
            grid.SourcesEnabled = Era < 2;
            grid.InheritedWatts = InheritedWattsFor(Era);
            // Until an era with lines sets it (Era 3 on), contracts take the station's output.
            if (GameManager.Instance != null) GameManager.Instance.ContractSupplyFactor = 1.0;
        }
    }
}
