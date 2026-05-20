using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Handles the crank-tap mechanic for Era 1 (§5.1.1).
    // CTV (Crank Tap Value) starts at 1 W/tap; each tap adds CTV joules.
    // No stamina or rate cap — idle generators dominate by minute 5.
    public class Era1TapController : MonoBehaviour
    {
        private double _crankTapValue = 1.0; // watts per tap

        // Each tap is treated as delivering CTV watts for 1 second (= CTV joules).
        private const double TapDurationSeconds = 1.0;

        private Economy.CurrencyLedger _ledger;
        private Simulation.PowerGrid   _grid;

        private void Start()
        {
            _ledger = Managers.GameManager.Instance?.Ledger;
            _grid   = Simulation.SimulationTick.Instance?.Grid;

            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
        }

        // Wired to the crank button's OnClick.
        public void OnCrankTapped()
        {
            double joules = _crankTapValue * TapDurationSeconds;
            _ledger?.AddJoules(joules);
            _grid?.AddTapEnergy(joules);
            EventBus.Publish(new TapEvent(_crankTapValue, joules));
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            var def = evt.Definition;
            if (def.effect == Models.UpgradeEffect.MultiplyCrankTapValue)
                _crankTapValue *= def.effectValue;
        }

        public double CrankTapValue => _crankTapValue;

        // Used by SaveManager on load.
        public void SetCrankTapValue(double value) => _crankTapValue = value;
    }
}
