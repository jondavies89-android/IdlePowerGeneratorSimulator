using System;
using UnityEngine;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Simulation
{
    // Drives the 1 Hz simulation tick (§8.1).
    // UI reads state from Grid; never writes to it.
    public class SimulationTick : MonoBehaviour
    {
        public static SimulationTick Instance { get; private set; }

        public PowerGrid   Grid   { get; private set; }
        public DemandModel Demand { get; private set; }

        private float _accumulator;
        private const float TickInterval = 1.0f;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Grid   = new PowerGrid();
            Demand = new DemandModel(0, new SeededRandom(seed: 1001));
        }

        private void Update()
        {
            _accumulator += Time.deltaTime;
            while (_accumulator >= TickInterval)
            {
                _accumulator -= TickInterval;
                DoTick(TickInterval);
            }
        }

        // §7.1.1 short-absence catch-up: tick at 10× until current.
        // For medium/long absences the SaveManager uses algebraic offline compute instead.
        public void CatchUpOffline(double secondsElapsed)
        {
            const double stepSize = 1.0;
            double remaining = secondsElapsed;
            while (remaining > 0)
            {
                double step = Math.Min(stepSize, remaining);
                DoTick(step);
                remaining -= step;
            }
        }

        private void DoTick(double delta)
        {
            double gameTime = GameClock.Instance != null ? GameClock.Instance.GameTimeSeconds : 0;
            float  era      = Managers.EraManager.Instance != null
                              ? Managers.EraManager.Instance.CurrentEra : 1f;

            double demand = Demand.Evaluate(gameTime, era);
            Grid.Tick(delta, demand);

            // The ledger is the one spendable store of joules (taps and purchases already go
            // through it), so generator output has to land there too.
            var eco    = Managers.GameManager.Instance;
            if (eco != null && Grid.LastTickSurplusJoules > 0)
                eco.Ledger.AddJoules(Grid.LastTickSurplusJoules);

            // Contract ledger needs the current output to record per-contract performance
            var ledger = eco?.Contracts;
            if (ledger != null && eco != null)
            {
                var cfg = eco.GetEraConfig((int)era);
                double baseRate = cfg != null ? cfg.contractBaseRate : 0.001;
                ledger.Tick(Grid.TotalOutputWatts, delta, gameTime, eco.Ledger, baseRate);
            }

            EventBus.Publish(new SimTickEvent(Grid, delta));
        }
    }
}
