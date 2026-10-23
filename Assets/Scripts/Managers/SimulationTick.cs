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

        public PowerGrid    Grid     { get; private set; }
        public DemandModel  Demand   { get; private set; }
        public Era1Flywheel Flywheel { get; private set; }

        private float _accumulator;
        private const float TickInterval = 1.0f;

        // Lets the 60 Hz UI interpolate between 1 Hz ticks.
        public float SecondsSinceTick => _accumulator;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Grid   = new PowerGrid();
            Demand = new DemandModel(0, new SeededRandom(seed: 1001));

            // Idle (no capacity, no floor) until Era 1 upgrades set it up.
            Flywheel = new Era1Flywheel(Data.Era1Data.FlywheelBaseDecayPerSecond);
            Grid.AddSource(Flywheel);
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
                // Contract durations are in game time, so their delivered energy must be too:
                // one real-second tick covers GameTimeScale game seconds. Using the real delta
                // left every contract at 1/60 of its target, i.e. always failed.
                double gameDelta = delta * (GameClock.Instance != null ? GameClock.Instance.GameTimeScale : 1f);
                ledger.Tick(Grid.TotalOutputWatts, gameDelta, gameTime, eco.Ledger, baseRate);
            }

            EventBus.Publish(new SimTickEvent(Grid, delta));
        }
    }
}
