using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Managers
{
    // Tracks and advances the current era.
    // Era 1 → 2 transition fires when the player earns their first penny (first customer);
    // Era 2 → 3 when the Great Exhibition is lit. Era 3 isn't built yet, so the Era 2 screen
    // stays up beyond it.
    public class EraManager : MonoBehaviour
    {
        public static EraManager Instance { get; private set; }

        public float CurrentEra { get; private set; } = 1f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            EventBus.Subscribe<ContractCompletedEvent>(OnContractCompleted);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ContractCompletedEvent>(OnContractCompleted);
        }

        private void OnContractCompleted(ContractCompletedEvent evt)
        {
            // First completed contract (which pays pennies) triggers Era 2.
            if (CurrentEra < 2f && evt.PenniesEarned > 0m)
                AdvanceTo(2);
            // Lighting the Great Exhibition closes Era 2.
            else if (CurrentEra < 3f && evt.Contract.Definition.contractId == Data.Era2Data.ExhibitionContractId)
                AdvanceTo(3);
        }

        private void AdvanceTo(int era)
        {
            int from = (int)CurrentEra;
            CurrentEra = era;
            EventBus.Publish(new EraAdvancedEvent(from, era));
            Debug.Log($"[EraManager] Advanced to Era {era}");
        }

        public void SetEraFromSave(float era) => CurrentEra = era;
    }
}
