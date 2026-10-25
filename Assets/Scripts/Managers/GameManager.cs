using System.Collections.Generic;
using UnityEngine;

namespace IdlePowerGen.Managers
{
    // Central wiring point. Owns the Economy layer objects and holds the ScriptableObject
    // catalogues populated in the Unity Inspector.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("ScriptableObject Catalogues")]
        [SerializeField] private Models.EraConfig[]          _eraConfigs;
        [SerializeField] private Models.UpgradeDefinition[]  _upgradeDefinitions;
        [SerializeField] private Models.GeneratorDefinition[] _generatorDefinitions;
        [SerializeField] private Models.ContractDefinition[]  _contractDefinitions;

        // Browser tabs are usually closed rather than quit, so pause/quit hooks alone
        // would lose progress on Web. A JSON write this small is cheap at this interval.
        [SerializeField] private float _autosaveIntervalSeconds = 10f;
        private float _autosaveTimer;

        public Economy.CurrencyLedger   Ledger    { get; private set; }
        public Economy.UpgradeRegistry  Upgrades  { get; private set; }
        public Economy.ContractLedger   Contracts { get; private set; }
        public Economy.StreetTrade      Street    { get; } = new();

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Ledger    = new Economy.CurrencyLedger();
            Upgrades  = new Economy.UpgradeRegistry(
                            new List<Models.UpgradeDefinition>(_upgradeDefinitions), Ledger);
            Contracts = new Economy.ContractLedger();
        }

        private void Start()
        {
            Save.SaveManager.Instance?.Load();
        }

        private void Update()
        {
            _autosaveTimer += Time.unscaledDeltaTime;
            if (_autosaveTimer < _autosaveIntervalSeconds) return;
            _autosaveTimer = 0f;
            Save.SaveManager.Instance?.Save();
        }

        // Auto-save on pause (§8.5)
        private void OnApplicationPause(bool paused)
        {
            if (paused) Save.SaveManager.Instance?.Save();
        }

        // Switching tabs or apps on Web/Android often arrives as a focus loss, not a pause.
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Save.SaveManager.Instance?.Save();
        }

        private void OnApplicationQuit()
        {
            Save.SaveManager.Instance?.Save();
        }

        public Models.EraConfig GetEraConfig(int era)
        {
            foreach (var cfg in _eraConfigs)
                if (cfg.eraNumber == era) return cfg;
            return null;
        }

        // Pay rate for a contract: its own era's, so the neighbour's Era 1 shilling and an
        // Era 2 engagement each pay what they were offered at.
        public double ContractBaseRate(Models.ContractDefinition def)
        {
            var cfg = GetEraConfig(Mathf.Max(1, def.minEra));
            return cfg != null ? cfg.contractBaseRate : 0.001;
        }

        // ---- Contracts 2× boost ----
        // Stands in for a rewarded ad in the published game: one tap (one ad) runs the game
        // clock at double speed for a while, so engagements finish twice as fast and still end
        // at the hour their card shows. Fifteen minutes is the usual length of an idle game's
        // ad boost, long enough to see a couple of engagements through. The street, wages and
        // coal keep to the unboosted pace (GameClock.BaseTimeScale), so only contracts gain.
        public const double ContractBoostSpeed   = 2.0;
        public const double ContractBoostSeconds = 15 * 60;

        // Real seconds of boost left. Counted down by the sim tick, so catch-up after a short
        // absence spends it too and a boost can't be banked by closing the tab.
        public double ContractBoostSecondsLeft { get; private set; }
        public bool   ContractBoostActive => ContractBoostSecondsLeft > 0;
        public double ContractSpeed => ContractBoostActive ? ContractBoostSpeed : 1.0;

        // Another tap tops it back up to the full length rather than stacking.
        public void StartContractBoost() => SetBoost(ContractBoostSeconds);

        public void TickContractBoost(double realSeconds) =>
            SetBoost(System.Math.Max(0, ContractBoostSecondsLeft - realSeconds));

        public void SetContractBoostFromSave(double seconds) =>
            SetBoost(System.Math.Clamp(seconds, 0, ContractBoostSeconds));

        private void SetBoost(double secondsLeft)
        {
            ContractBoostSecondsLeft = secondsLeft;
            var clock = Utils.GameClock.Instance;
            if (clock != null) clock.SpeedMultiplier = (float)ContractSpeed;
        }

        // Share of the station's output that reaches a contract client (Era 3 on: the lines
        // lose the rest). Set each tick by the era's works from its last share-out.
        public double ContractSupplyFactor { get; set; } = 1.0;

        // What the street pays for a watt-hour of spare power at this time of day, in pennies.
        // averageDay prices a whole day at once (the daily shape averages ≈ 1), for offline pay.
        public double StreetPricePerWattHour(float era, double gameTimeSeconds, bool averageDay = false)
        {
            // The latest era built so far: after the Exhibition (Era 3, not yet made) the
            // works keeps running, and the street keeps buying, on Era 2's terms.
            Models.EraConfig cfg = null;
            foreach (var c in _eraConfigs)
                if (c != null && c.eraNumber <= era && (cfg == null || c.eraNumber > cfg.eraNumber)) cfg = c;
            if (cfg == null || cfg.streetRateFactor <= 0) return 0;
            double shape = averageDay ? 1.0
                         : Simulation.DemandModel.DailyShape(gameTimeSeconds / 3600.0 % 24.0);
            double upgrades = UpgradeEffectApplier.Instance != null ? UpgradeEffectApplier.Instance.StreetTradeMultiplier : 1.0;
            return cfg.contractBaseRate * cfg.streetRateFactor * shape * upgrades;
        }

        public Models.ContractDefinition FindContract(string contractId)
        {
            if (_contractDefinitions == null) return null;
            foreach (var def in _contractDefinitions)
                if (def != null && def.contractId == contractId) return def;
            return null;
        }

        public IReadOnlyList<Models.GeneratorDefinition> GeneratorCatalogue => _generatorDefinitions;
        public IReadOnlyList<Models.UpgradeDefinition>   UpgradeCatalogue   => _upgradeDefinitions;
        public IReadOnlyList<Models.ContractDefinition>  ContractCatalogue  => _contractDefinitions;
    }
}
