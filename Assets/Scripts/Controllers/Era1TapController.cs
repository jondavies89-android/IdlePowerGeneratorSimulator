using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Handles the crank-tap mechanic for Era 1 (§5.1.1).
    // A turn of the crank pays its joules at once and, once there is an iron flywheel,
    // adds the same number of watts of spin to it. Magnets multiply what the turn pays;
    // they act on the dynamo, not on the spin the wheel stores.
    public class Era1TapController : MonoBehaviour
    {
        private double _turnJoules = Data.Era1Data.InitialCrankTapValue;

        private Economy.CurrencyLedger    Ledger   => Managers.GameManager.Instance?.Ledger;
        private Simulation.Era1Flywheel   Flywheel => Simulation.SimulationTick.Instance?.Flywheel;

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
            double joules = CrankTapValue;
            Ledger?.AddJoules(joules);
            var sim = Simulation.SimulationTick.Instance;
            sim?.Flywheel.AddSpin(_turnJoules, sim.SecondsSinceTick);
            EventBus.Publish(new TapEvent(joules, joules));
        }

        private void OnUpgradePurchased(UpgradePurchasedEvent evt) => ApplyLevel(evt.Definition, evt.NewLevel);

        private void OnGameLoaded(GameLoadedEvent _) => RecomputeFromPurchased();

        // level: the level just reached. The crank's bonus grows with it (+1, +2, +3 …).
        private void ApplyLevel(Models.UpgradeDefinition def, int level)
        {
            if (def.effect == Models.UpgradeEffect.AddCrankJoules)
                _turnJoules += def.effectValue * level;
            else if (def.effect == Models.UpgradeEffect.MultiplyCrankTapValue)
                _turnJoules *= def.effectValue;
        }

        // The turn's value isn't saved; it is rebuilt from every purchased crank level.
        private void RecomputeFromPurchased()
        {
            var gm = Managers.GameManager.Instance;
            _turnJoules = Data.Era1Data.InitialCrankTapValue;
            if (gm == null) return;

            foreach (var def in gm.UpgradeCatalogue)
            {
                int level = gm.Upgrades.Level(def.upgradeId);
                for (int i = 1; i <= level; i++) ApplyLevel(def, i);
            }
        }

        // Joules one turn pays, magnets included.
        public double CrankTapValue => _turnJoules * (Flywheel?.OutputMultiplier ?? 1.0);

        // Watts of spin one turn adds to the flywheel.
        public double SpinPerTurn => _turnJoules;
    }
}
