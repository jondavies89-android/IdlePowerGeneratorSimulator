using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // What Eras 3–5 share: load areas (districts, feeders, cities) that pay a tariff on what
    // reaches them, research from R&D buildings, the era's goal and capstone, its date, and
    // its contract board. Each era says how its areas lose power and what the station can send.
    public abstract class GridWorksController : ContractDesk, Save.ISaveParticipant, Save.IOfflineEarner
    {
        public IReadOnlyList<LoadArea> Areas => _areas;
        protected readonly List<LoadArea> _areas = new();

        // Game day the era began on: the date shown counts on from the era's start date.
        public long StartDay { get; private set; }

        // Most delivered at once this era, contracts included: the goal bar's reading.
        public double BestDeliveredWatts { get; private set; }
        public bool   CapstoneReady { get; private set; }

        // ---- The last tick, for the screen ----
        public double GeneratedWatts { get; protected set; } // sent from the station
        public double DeliveredWatts { get; protected set; } // reached customers, contracts included
        public double LossWatts      { get; protected set; }
        public double DemandWatts    { get; protected set; } // what the connected areas asked for
        public double IncomePerHour  { get; protected set; } // pennies per game hour, tariff and contracts
        public TransmissionNetwork.Result LastShare { get; protected set; }

        public double LossFraction => GeneratedWatts > 0 ? LossWatts / GeneratedWatts : 0;

        protected abstract DateTime StartDate { get; }
        protected abstract float StartHour { get; }

        public Models.EraConfig Config => GM != null ? GM.GetEraConfig(EraNumber) : null;
        public double GoalWatts => Config != null ? Config.maxWatts : double.MaxValue;

        protected virtual void Awake()
        {
            CreateAreas();
            EventBus.Subscribe<SimTickEvent>(OnSimTick);
            EventBus.Subscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        protected virtual void OnDestroy()
        {
            EventBus.Unsubscribe<SimTickEvent>(OnSimTick);
            EventBus.Unsubscribe<EraAdvancedEvent>(OnEraAdvanced);
        }

        // Builds the era's load areas from its data, all unconnected.
        protected abstract void CreateAreas();

        // Sets each area's LossPerWatt (and ReactivePerWatt) for this tick.
        protected abstract void UpdateLines();

        // ---- Era start ----

        private void OnEraAdvanced(EraAdvancedEvent e)
        {
            if (e.ToEra != EraNumber) return;
            // Forward to the era's start hour on the next day, never back.
            long day = Economy.ContractBoard.DayOf(Now) + 1;
            GameClock.Instance?.SetGameTime(day * Economy.ContractBoard.SecondsPerDay + StartHour * 3600.0);
            StartDay = day;
            BestDeliveredWatts = 0;
            CapstoneReady = false;
            Board.LoadFromSave(null);
            foreach (var a in _areas) { a.Connected = false; a.Shed = false; }
            ResetAreaExtras();
            OnEraStart();
        }

        // The era's first customers, already on the mains when it opens.
        protected abstract void OnEraStart();

        public DateTime Date => StartDate.AddDays(Math.Max(0, Economy.ContractBoard.DayOf(Now) - StartDay));

        // ---- The tick ----

        private void OnSimTick(SimTickEvent e)
        {
            if (!IsCurrentEra || GM == null) return;
            // Tariffs and research at the unboosted pace, like Era 2's street (the 2× boost
            // finishes contracts sooner; it doesn't double the town's bills).
            double scale = GameClock.Instance != null ? GameClock.Instance.BaseTimeScale : 1f;
            double hours = e.DeltaTime * scale / 3600.0;
            double hourOfDay = Now / 3600.0 % 24.0;

            foreach (var a in _areas)
                a.DemandWatts = a.Supplied ? a.BaseWatts * AreaShape(a, hourOfDay) : 0;
            UpdateLines();

            // Contracts were served in the sim tick from what reaches the far end of a line;
            // the towns share what is left of the station's output.
            double factor = GM.ContractSupplyFactor;
            double contractDelivered = GM.Contracts.DeliveredWatts;
            double contractSent = factor > 0 ? contractDelivered / factor : 0;
            double townsWant = 0;
            foreach (var a in _areas)
                if (a.Supplied)
                {
                    double d = Math.Max(0, a.DemandWatts);
                    townsWant += d + TransmissionNetwork.EffectiveLoss(a) * d * d;
                }
            double station = Dispatch(townsWant + contractSent, e.DeltaTime, hours);
            var share = TransmissionNetwork.Share(_areas, Math.Max(0, station - contractSent));
            LastShare = share;

            GeneratedWatts = share.Sent + contractSent;
            LossWatts      = share.Losses + (contractSent - contractDelivered);
            DeliveredWatts = share.Delivered + contractDelivered;
            DemandWatts    = share.Demand;
            // Next tick's contracts lose what the towns lose now, on average.
            GM.ContractSupplyFactor = share.Sent > 0 ? 1 - share.LossFraction : 1 - StandingLossFraction;

            double paid = 0;
            foreach (var a in _areas) if (a.Supplied) paid += a.DeliveredWatts * PayFactor(a);
            decimal pay = (decimal)(paid * Tariff * hours);
            if (pay > 0) GM.Ledger.AddPennies(pay);
            if (hours > 0) IncomePerHour = (double)(pay + GM.Contracts.PaidLastTick) / hours;

            var applier = Managers.UpgradeEffectApplier.Instance;
            if (applier != null) GM.Ledger.AddResearch(applier.ResearchPerHour * hours);

            AfterShare(hours);

            BestDeliveredWatts = Math.Max(BestDeliveredWatts, DeliveredWatts);
            if (!CapstoneReady && BestDeliveredWatts >= GoalWatts)
            {
                CapstoneReady = true;
                EventBus.Publish(new CapstoneReadyEvent(EraNumber));
            }
        }

        // What the station can send this tick. Era 3: everything the machines make.
        protected virtual double StationWatts() => Simulation.SimulationTick.Instance?.Grid.TotalOutputWatts ?? 0;

        // Runs the station against what the towns and contracts would take (sent watts, losses
        // included) and returns what it sends. Era 3's machines simply make their output;
        // Eras 4–5 dispatch their units and the frequency answers. dt is real seconds.
        protected virtual double Dispatch(double wantedWatts, double dt, double gameHours) => StationWatts();

        // Losses a contract client would see when no town is being supplied.
        protected virtual double StandingLossFraction => 0.05;

        // Share of the tariff an area pays for what reaches it (Era 5: less when its voltage sags).
        protected virtual double PayFactor(LoadArea area) => 1.0;

        // Era-specific work after the share-out (frequency, fuel, alerts).
        protected virtual void AfterShare(double gameHours) { }

        // Demand shape through the day for one area. The bible's daily curve (§3.4) for all.
        protected virtual double AreaShape(LoadArea area, double hourOfDay) => DemandModel.DailyShape(hourOfDay);

        // Pennies per watt-hour delivered, upgrades included.
        public double Tariff
        {
            get
            {
                var cfg = Config;
                double mult = Managers.UpgradeEffectApplier.Instance != null ? Managers.UpgradeEffectApplier.Instance.TariffMultiplier : 1.0;
                return (cfg != null ? cfg.tariffPenniesPerWattHour : 0) * mult;
            }
        }

        // ---- Contracts ----

        // Contract clients are served before the towns (they pay better, and they're booked),
        // so what a new one could have is the station's output that reaches the end of a line,
        // less what the other contracts already take. The towns dim to make room.
        public override double SpareForContracts =>
            StationWatts() * (GM != null ? GM.ContractSupplyFactor : 1) - CommittedWatts;

        // ---- Capstone ----

        // Closes the era (the capstone card's button).
        public void TakeCapstone()
        {
            if (!CapstoneReady) return;
            int next = EraNumber + 1;
            // Past the last era built: the end-of-content card, and the grid runs on.
            if (next > Managers.EraManager.LastBuiltEra) { EventBus.Publish(new ContentEndEvent(EraNumber)); return; }
            Managers.EraManager.Instance?.AdvanceTo(next);
        }

        // ---- Areas ----

        public LoadArea Area(string id) => _areas.FirstOrDefault(a => a.Id == id);

        // ---- Save ----

        public virtual void WriteSave(Save.SaveData data)
        {
            if (!IsCurrentEra) return;
            data.eraStartDay = StartDay;
            data.capstoneOffered = CapstoneReady;
            foreach (var a in _areas)
            {
                var entry = new Save.SaveData.LoadAreaEntry { id = a.Id, connected = a.Connected, shed = a.Shed };
                WriteArea(a, entry);
                data.loadAreas.Add(entry);
            }
            foreach (var (id, day) in Board.BookedDays)
                data.contractBookedDays.Add(new Save.SaveData.BookedDayEntry { contractId = id, day = day });
        }

        public virtual void ReadSave(Save.SaveData data)
        {
            foreach (var a in _areas) { a.Connected = false; a.Shed = false; }
            ResetAreaExtras();
            BestDeliveredWatts = 0;
            CapstoneReady = false;
            IncomePerHour = 0;
            Board.LoadFromSave(null);
            if ((int)data.currentEra != EraNumber) return;

            StartDay = data.eraStartDay;
            CapstoneReady = data.capstoneOffered;
            var booked = new List<(string, long)>();
            foreach (var e in data.contractBookedDays ?? new List<Save.SaveData.BookedDayEntry>())
                booked.Add((e.contractId, e.day));
            Board.LoadFromSave(booked);

            // A save made before the era had begun its own state (an Era 2 save past the
            // Exhibition, from before Era 3 existed) starts the era as if it had just opened.
            if (data.loadAreas == null || data.loadAreas.Count == 0)
            {
                StartDay = Economy.ContractBoard.DayOf(data.gameTimeSeconds);
                OnEraStart();
                return;
            }
            foreach (var entry in data.loadAreas)
            {
                var a = Area(entry.id);
                if (a == null) continue;
                a.Connected = entry.connected;
                a.Shed = entry.shed;
                ReadArea(a, entry);
            }
        }

        // An area's era-specific state (copper, capacitor banks) for the save.
        protected virtual void WriteArea(LoadArea area, Save.SaveData.LoadAreaEntry entry) { }
        protected virtual void ReadArea(LoadArea area, Save.SaveData.LoadAreaEntry entry) { }
        protected virtual void ResetAreaExtras() { }

        // ---- Offline (bible §7) ----

        // A medium or long absence: the clock waits for the player, and the connected areas
        // that were being supplied at save time keep paying for it on the decaying curve.
        // Nothing breaks while the player is away (CLAUDE.md, bible §10.5).
        public decimal OfflineEarnings(Save.SaveData data, double realSeconds, float eraFactor, float tauHours)
        {
            if ((int)data.currentEra != EraNumber || GM == null) return 0m;
            // An average day's demand (the daily shape averages ≈ 1) shared out once.
            double capacity = OfflineCapacityWatts();
            foreach (var a in _areas) a.DemandWatts = a.Supplied ? a.BaseWatts : 0;
            UpdateLines();
            double served = TransmissionNetwork.Share(_areas, capacity).Delivered;
            double scale = GameClock.Instance != null ? GameClock.Instance.BaseTimeScale : 60f;
            double joules = Managers.OfflineProgressionManager.ComputeOfflineJoules(served, realSeconds, eraFactor, tauHours);
            var applier = Managers.UpgradeEffectApplier.Instance;
            if (applier != null) GM.Ledger.AddResearch(applier.ResearchPerHour * realSeconds * scale / 3600.0 * 0.7);
            return (decimal)(joules * scale / 3600.0 * Tariff);
        }

        // What keeps running untended. Era 3: every machine; Eras 4–5: the units online.
        protected virtual double OfflineCapacityWatts() =>
            Simulation.SimulationTick.Instance?.Grid.ComputeSustainedOutputWatts() ?? 0;
    }
}
