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
        public Era1Flywheel   Flywheel   { get; private set; }
        public Era1WaterWheel WaterWheel { get; private set; }

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

            // Both idle (no capacity, no watts) until Era 1 upgrades set them up.
            Flywheel   = new Era1Flywheel(Data.Era1Data.FlywheelBaseDecayPerSecond);
            WaterWheel = new Era1WaterWheel();
            Grid.AddSource(Flywheel);
            Grid.AddSource(WaterWheel);
        }

        // At the DEV strip's top speed a frame holds ~17 ticks; past this many the frame gives
        // up the backlog rather than falling further behind each frame.
        private const int MaxTicksPerFrame = 200;

        // Held while a minigame runs on its own clock.
        public bool Paused { get; set; }

        private void Update()
        {
            if (Paused) return;
            _accumulator += Time.deltaTime * DevTime.Multiplier;
            int ticks = 0;
            while (_accumulator >= TickInterval)
            {
                _accumulator -= TickInterval;
                DoTick(TickInterval);
                if (++ticks >= MaxTicksPerFrame) { _accumulator = 0f; break; }
            }
        }

        // §7.1.1 short-absence catch-up: tick at 10× until current.
        // For medium/long absences the SaveManager uses algebraic offline compute instead.
        public void CatchUpOffline(double secondsElapsed)
        {
            const double stepSize = 1.0;
            double remaining = secondsElapsed;
            var contracts = Managers.GameManager.Instance?.Contracts;
            if (contracts != null) contracts.ShortfallPaused = true;
            try
            {
                while (remaining > 0)
                {
                    double step = Math.Min(stepSize, remaining);
                    // The clock moves with the ticks, so contracts end on time rather than
                    // soaking up minutes of output past their last hour.
                    var clock = GameClock.Instance;
                    clock?.SetGameTime(clock.GameTimeSeconds + step * clock.GameTimeScale);
                    DoTick(step);
                    remaining -= step;
                }
            }
            finally
            {
                if (contracts != null) contracts.ShortfallPaused = false;
            }
        }

        private void DoTick(double delta)
        {
            double gameTime = GameClock.Instance != null ? GameClock.Instance.GameTimeSeconds : 0;
            float  era      = Managers.EraManager.Instance != null
                              ? Managers.EraManager.Instance.CurrentEra : 1f;

            double demand = Demand.Evaluate(gameTime, era);
            Grid.Tick(delta, demand);

            // Contract ledger needs the current output to record per-contract performance
            var eco    = Managers.GameManager.Instance;
            var ledger = eco?.Contracts;
            double delivered = 0;
            if (ledger != null)
            {
                // Contract durations are in game time, so their delivered energy must be too:
                // one real-second tick covers GameTimeScale game seconds. Using the real delta
                // left every contract at 1/60 of its target, i.e. always failed.
                var clock = GameClock.Instance;
                double gameDelta = delta * (clock != null ? clock.GameTimeScale : 1f);
                // From Era 3 a contract client is at the far end of a line, so it gets what
                // survives the losses; the era's works shares out what the contracts leave.
                double contractSupply = Grid.TotalOutputWatts * eco.ContractSupplyFactor;
                ledger.Tick(contractSupply, gameDelta, gameTime, eco.Ledger, eco.ContractBaseRate);
                delivered = eco.ContractSupplyFactor > 0 ? ledger.DeliveredWatts / eco.ContractSupplyFactor : 0;

                // Whatever the engagements left goes to the street, where there is a street.
                // At the unboosted pace: under the 2× boost the clock runs double, and each
                // game hour sells half the energy, so the street pays the same per real second
                // and only the contracts gain from the boost.
                double streetDelta = delta * (clock != null ? clock.BaseTimeScale : 1f);
                eco.Street.Sell(Grid.TotalOutputWatts - delivered, streetDelta,
                                eco.StreetPricePerWattHour(era, gameTime), eco.Ledger);
                eco.TickContractBoost(delta);
                delivered += eco.Street.LastWatts;
            }

            // The ledger is the one spendable store of joules (taps and purchases already go
            // through it), so generator output has to land there too, less what was sold.
            double banked = Grid.LastTickSurplusJoules - delivered * delta;
            if (eco != null && banked > 0)
                eco.Ledger.AddJoules(banked);

            EventBus.Publish(new SimTickEvent(Grid, delta));
        }
    }
}
