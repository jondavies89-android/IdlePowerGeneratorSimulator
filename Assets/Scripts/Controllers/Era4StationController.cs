using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Simulation;
using IdlePowerGen.Utils;

namespace IdlePowerGen.Controllers
{
    // Era 4, the Grid Awakens: the Central Electric Station's control desk. The city's feeders
    // draw on the daily curve, the units share the load through their governors, and the
    // frequency needle shows how well they're keeping up.
    public class Era4StationController : UnitGridController
    {
        public override int EraNumber => Era4Data.EraNumber;
        protected override int BaseSlots => Era4Data.BaseContractSlots;
        protected override DateTime StartDate => new(Era4Data.StartYear, Era4Data.StartMonth, Era4Data.StartDay);
        protected override float StartHour => Era4Data.StartHour;

        private readonly Dictionary<string, Era4Data.FeederSpec> _specs = new();

        protected override void CreateAreas()
        {
            foreach (var s in Era4Data.Feeders)
            {
                _specs[s.Id] = s;
                _areas.Add(new LoadArea(s.Id, s.BaseMegawatts * 1e6, 1));
            }
        }

        protected override void ConnectStartingAreas()
        {
            foreach (var s in Era4Data.Feeders)
                if (s.StartsOn) Area(s.Id).Connected = true;
        }

        public Era4Data.FeederSpec Spec(string id) => _specs[id];
        public override bool IsIndustrial(string areaId) => _specs.TryGetValue(areaId, out var s) && s.Industrial;
        public override string AreaName(string areaId) => _specs.TryGetValue(areaId, out var s) ? s.Name : areaId;

        // The city's own lines lose a steady few per cent; distance is Era 3's lesson.
        protected override void UpdateLines()
        {
            foreach (var a in _areas) a.LossPerWatt = 0.04 / Math.Max(1, a.BaseWatts);
        }

        // ---- Feeders ----

        public decimal ConnectCost(string id) => Managers.Money.FromPounds(_specs[id].ConnectPounds);

        public bool CanConnect(string id) =>
            IsCurrentEra && BlackStart == null && !Area(id).Connected && GM.Ledger.Pennies >= ConnectCost(id);

        // The feeder is switched in: its whole load arrives at once, and the needle dips.
        public bool Connect(string id)
        {
            if (!CanConnect(id) || !GM.Ledger.SpendPennies(ConnectCost(id))) return false;
            Area(id).Connected = true;
            EventBus.Publish(new WorksNoticeEvent($"{_specs[id].Name} is switched in."));
            return true;
        }

        // The city's peak tonight against what's online: the warning before the evening.
        public double PeakDemandWatts =>
            _areas.Where(a => a.Connected).Sum(a => a.BaseWatts) * 1.42 * 1.04 + CommittedWatts;
    }
}
