using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Handles the crank-tap mechanic for Era 1 (§5.1.1).
    // CTV (Crank Tap Value) starts at 1 W/tap; each tap adds CTV joules.
    // No stamina or rate cap — idle generators dominate by minute 5.
    public class Era1TapController : MonoBehaviour
    {
        private double _crankTapValue = Data.Era1Data.InitialCrankTapValue; // watts per tap

        // Each tap is treated as delivering CTV watts for 1 second (= CTV joules).
        private const double TapDurationSeconds = 1.0;

        private Economy.CurrencyLedger Ledger => Managers.GameManager.Instance?.Ledger;

        // Subscribed in Awake so a load that happens during another object's Start
        // can't slip past before this controller is listening.
        private void Awake()
        {
            EventBus.Subscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Subscribe<GameLoadedEvent>(OnGameLoaded);
        }

        private void Start() => RecomputeFromPurchased();

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UpgradePurchasedEvent>(OnUpgradePurchased);
            EventBus.Unsubscribe<GameLoadedEvent>(OnGameLoaded);
        }

        // Wired to the crank button's OnClick.
        public void OnCrankTapped()
        {
            double joules = _crankTapValue * TapDurationSeconds;
            Ledger?.AddJoules(joules);
            EventBus.Publish(new TapEvent(_crankTapValue, joules));
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt)
        {
            var def = evt.Definition;
            if (def.effect == Models.UpgradeEffect.MultiplyCrankTapValue)
                _crankTapValue *= def.effectValue;
        }

        private void OnGameLoaded(GameLoadedEvent _) => RecomputeFromPurchased();

        // CTV isn't saved; it is the starting value times every purchased crank multiplier.
        private void RecomputeFromPurchased()
        {
            var gm = Managers.GameManager.Instance;
            _crankTapValue = Data.Era1Data.InitialCrankTapValue;
            if (gm == null) return;

            foreach (var def in gm.UpgradeCatalogue)
            {
                if (def.effect == Models.UpgradeEffect.MultiplyCrankTapValue
                    && gm.Upgrades.IsPurchased(def.upgradeId))
                    _crankTapValue *= def.effectValue;
            }
        }

        public double CrankTapValue => _crankTapValue;
    }
}
