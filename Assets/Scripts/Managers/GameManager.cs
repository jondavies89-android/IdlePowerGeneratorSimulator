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
