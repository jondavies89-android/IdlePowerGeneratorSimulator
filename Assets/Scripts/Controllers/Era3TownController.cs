using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Era 3, the War of Currents: the town of Westgate and its districts. Districts are
    // lit one at a time, out from the station along the streets; what each one loses on the
    // way depends on its size, its distance, the mains (Edison's DC, then Westinghouse's AC)
    // and how heavy its copper is.
    public class Era3TownController : GridWorksController
    {
        public override int EraNumber => Era3Data.EraNumber;
        protected override int BaseSlots => Era3Data.BaseContractSlots;
        protected override DateTime StartDate => new(Era3Data.StartYear, Era3Data.StartMonth, Era3Data.StartDay);
        protected override float StartHour => Era3Data.StartHour;

        private readonly Dictionary<string, Era3Data.DistrictSpec> _specs = new();
        private readonly Dictionary<string, int> _copper = new();

        protected override void CreateAreas()
        {
            foreach (var s in Era3Data.Districts)
            {
                _specs[s.Id] = s;
                _areas.Add(new LoadArea(s.Id, s.BaseKilowatts * 1e3, s.Blocks));
            }
        }

        protected override void OnEraStart()
        {
            foreach (var s in Era3Data.Districts)
                if (s.StartsLit) Area(s.Id).Connected = true;
            Boiler.Reset();
        }

        public Era3Data.DistrictSpec Spec(string id) => _specs[id];

        // ---- Mains ----

        public int MainsTier
        {
            get
            {
                var applier = Managers.UpgradeEffectApplier.Instance;
                int tier = applier != null ? applier.MainsTier : 0;
                return Math.Clamp(tier, 0, Era3Data.Mains.Length - 1);
            }
        }

        public Era3Data.MainsSpec Mains => Era3Data.Mains[MainsTier];

        public int CopperLevel(string id) => _copper.TryGetValue(id, out var l) ? l : 0;

        // Fraction lost per watt delivered to this district (see TransmissionNetwork).
        public double LossPerWatt(string id, int? copperLevel = null, int? tier = null)
        {
            var spec = _specs[id];
            var mains = Era3Data.Mains[tier ?? MainsTier];
            double copper = Math.Pow(Era3Data.CopperLossFactor, copperLevel ?? CopperLevel(id));
            double mult = Managers.UpgradeEffectApplier.Instance != null ? Managers.UpgradeEffectApplier.Instance.LineLossMultiplier : 1.0;
            return mains.LossPerMegawattBlock * spec.Blocks * copper * mult / 1e6;
        }

        protected override void UpdateLines()
        {
            foreach (var a in _areas) a.LossPerWatt = LossPerWatt(a.Id);
        }

        // ---- Connecting ----

        public enum ConnectState { Lit, Available, TooFar, NeedsParent, CantAfford }

        // The district between this one and the station, or null for the station itself:
        // along the middle street, and up or down from it.
        public string ParentOf(string id)
        {
            var s = _specs[id];
            int col = s.Column, row = s.Row;
            int pc = row == Era3Data.StationRow ? col - 1 : col;
            int pr = Era3Data.StationRow;
            if (pc == Era3Data.StationColumn && pr == Era3Data.StationRow) return null;
            return Era3Data.Districts.First(d => d.Column == pc && d.Row == pr).Id;
        }

        public decimal ConnectCost(string id) => Managers.Money.FromPounds(_specs[id].ConnectPounds);

        public ConnectState StateOf(string id)
        {
            var area = Area(id);
            if (area.Connected) return ConnectState.Lit;
            string parent = ParentOf(id);
            if (parent != null && !Area(parent).Connected) return ConnectState.NeedsParent;
            if (_specs[id].Blocks > Mains.Reach) return ConnectState.TooFar;
            if (GM == null || GM.Ledger.Pennies < ConnectCost(id)) return ConnectState.CantAfford;
            return ConnectState.Available;
        }

        public bool Connect(string id)
        {
            if (!IsCurrentEra || StateOf(id) != ConnectState.Available) return false;
            if (!GM.Ledger.SpendPennies(ConnectCost(id))) return false;
            Area(id).Connected = true;
            EventBus.Publish(new WorksNoticeEvent($"{_specs[id].Name} is lit."));
            return true;
        }

        // ---- Copper ----

        public decimal CopperCost(string id) =>
            Managers.Money.FromPounds(Math.Max(_specs[id].ConnectPounds, Era3Data.ConnectPoundsPerKilowattBlock * _specs[id].BaseKilowatts)
                                      * Era3Data.CopperCostShare * (CopperLevel(id) + 1));

        public bool CanUpgradeCopper(string id) =>
            Area(id).Connected && CopperLevel(id) < Era3Data.MaxCopperLevel;

        public bool UpgradeCopper(string id)
        {
            if (!IsCurrentEra || !CanUpgradeCopper(id)) return false;
            if (!GM.Ledger.SpendPennies(CopperCost(id))) return false;
            _copper[id] = CopperLevel(id) + 1;
            return true;
        }

        // ---- What the screen warns about ----

        // The lit district losing most on its line, if it's bad enough that the lamps flicker.
        public LoadArea WorstFlicker =>
            _areas.Where(a => a.Supplied && a.LossFraction >= Era3Data.FlickerLoss)
                  .OrderByDescending(a => a.LossFraction).FirstOrDefault();

        // ---- Active play: the boilers and the galvanometer ----

        public Era3Boiler Boiler { get; } = new();
        public Era3Galvanometer Galvanometer { get; } = new(new SeededRandom(seed: 3301));

        // What stoking has added to the purse this session, and its rate while in the green.
        public decimal StokeEarned { get; private set; }
        public double  StokeBonusPerHour => Math.Max(0, IncomePerHour) * Era3Data.StokeIncomeBonus;

        private static double ResearchPerHour =>
            Managers.UpgradeEffectApplier.Instance != null ? Managers.UpgradeEffectApplier.Instance.ResearchPerHour : 0;

        public bool Stoke() => IsCurrentEra && Boiler.Stoke();

        public double ReadingWorth => Galvanometer.Worth(ResearchPerHour);

        public Era3Galvanometer.Reading TakeReading()
        {
            if (!IsCurrentEra || GM == null) return default;
            var reading = Galvanometer.Take(ResearchPerHour);
            if (reading.Hit) GM.Ledger.AddResearch(reading.Research);
            return reading;
        }

        // Real time, whatever the game clock or the DEV speed: they are played by hand.
        private void Update()
        {
            if (!IsCurrentEra) return;
            float dt = UnityEngine.Time.unscaledDeltaTime;
            Boiler.Tick(dt);
            Galvanometer.Tick(dt);
        }

        // Steam held in the green: the works earns twice as much, for the share of the
        // tick it was there.
        protected override void AfterShare(double gameHours)
        {
            double share = Boiler.TakeGreenShare();
            if (GM.Contracts.ShortfallPaused) return; // catching up on time away: nobody was stoking
            decimal bonus = (decimal)(StokeBonusPerHour * share * gameHours);
            if (bonus <= 0) return;
            GM.Ledger.AddPennies(bonus);
            StokeEarned += bonus;
        }

        // ---- Save ----

        protected override void WriteArea(LoadArea area, Save.SaveData.LoadAreaEntry entry) =>
            entry.copperLevel = CopperLevel(area.Id);

        protected override void ReadArea(LoadArea area, Save.SaveData.LoadAreaEntry entry)
        {
            if (entry.copperLevel > 0) _copper[area.Id] = Math.Min(entry.copperLevel, Era3Data.MaxCopperLevel);
        }

        protected override void ResetAreaExtras() => _copper.Clear();
    }
}
