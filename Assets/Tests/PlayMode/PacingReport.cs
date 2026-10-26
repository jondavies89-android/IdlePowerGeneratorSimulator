using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using IdlePowerGen.Controllers;
using IdlePowerGen.Models;

namespace IdlePowerGen.Tests
{
    // Not a test of behaviour: a greedy bot plays an era from its start in the real scene, at
    // sim speed, and logs how long the era took and its longest wait between purchases (bible
    // §2.1 era lengths, §6.2 1–3 minute waits early on). Run by name to tune Era*Data:
    //   -testFilter IdlePowerGen.Tests.PacingReport
    [Explicit, Category("Pacing")]
    public class PacingReport : Era1SceneFixture
    {
        private static Managers.GameManager GM => Managers.GameManager.Instance;
        private static Era2GeneratorController Generators => Object.FindAnyObjectByType<Era2GeneratorController>();

        private const int MaxRealSeconds = 3 * 24 * 3600;

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator Era2() { yield return Play(2, 1000); }

        [UnityTest]
        public IEnumerator Era2AtTheOldStart() { yield return Play(2, 1600); }

        [UnityTest]
        public IEnumerator Era3() { yield return Play(3, -1); }

        [UnityTest]
        public IEnumerator Era4() { yield return Play(4, -1); }

        [UnityTest]
        public IEnumerator Era5() { yield return Play(5, -1); }

        private static int _blackouts, _brownouts;
        private static bool _done;

        // Eras 4–5: keep reserve, sync at once, service and fuel on time, connect when there's room.
        private static bool ActGrid(UnitGridController g)
        {
            var gm = GM;
            if (g.BlackStart != null)
            {
                // A bot can't play the minigame; count it and restore by hand.
                _blackouts++;
                if (_blackouts <= 6) _log.Append($"\n  BLACKOUT: {g.LastBlackoutCause}");
                foreach (var a in g.Areas) a.Shed = false;
                typeof(UnitGridController).GetMethod("EndBlackStart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(g, new object[] { 5f });
                g.Grid.Reset();
                foreach (var u in g.AwaitingSync.ToList()) g.EndSync(u, Simulation.Synchroscope.Close.Clean);
                return false;
            }
            if (g.SheddingStage > 0) _brownouts++;
            // The cascade warning's remedies, as a player would take them.
            if (g.CascadeSecondsLeft >= 0 || g.Frequency < 49.3)
            {
                var biggest = g.BiggestLit;
                if (biggest != null && g.Areas.Count(a => a.Supplied) > 1) { g.SetShed(biggest.Id, true); _last = "drop " + g.AreaName(biggest.Id); return true; }
            }
            // Pick dropped areas up again once there is room.
            var dropped = g.Areas.FirstOrDefault(a => a.Connected && a.Shed);
            if (dropped != null && g.ReserveWatts > dropped.BaseWatts * 1.5) g.SetShed(dropped.Id, false);
            foreach (var u in g.AwaitingSync.ToList()) g.EndSync(u, Simulation.Synchroscope.Close.Clean);
            foreach (var u in g.Units.Where(u => !u.Inherited && u.State == Simulation.GridUnit.UnitState.Online &&
                                                 u.HoursSinceService > u.Definition.serviceIntervalHours * 0.9).ToList())
                g.Service(u);
            if (g.HasCoalUnits && g.CoalDaysOfCover < 2) g.OrderCoal();
            if (g.FuelManagerOpen && g.CoalTargetDays < 5) g.SetCoalTarget(6);
            if (g is Era5DispatchController e5)
            {
                var worst = e5.WorstCity;
                if (worst != null && e5.Voltage(worst.Id) < 0.95 && e5.CanAddBank(worst.Id)) { e5.AddBank(worst.Id); _last = "bank " + e5.AreaName(worst.Id); return true; }
                if (e5.NextToFit != null && e5.RedundancyScore < 70 && gm.Ledger.Pennies > e5.FitCost(e5.NextToFit) * 4) { e5.FitBlackStart(); _last = "fit black start"; return true; }
            }

            var grid = Simulation.SimulationTick.Instance.Grid;
            double online = g.Units.Where(u => u.IsOnline || u.State == Simulation.GridUnit.UnitState.InService)
                             .Sum(u => u.Capacity(grid)) * g.Grid.CapacityFactor;
            double peak = g.Areas.Where(a => a.Connected).Sum(a => a.BaseWatts) * 1.35 * 1.06 + g.CommittedWatts;
            var kinds = g.UnitKinds.Where(d => g.IsUnlocked(d) && !g.AtLimit(d)).ToList();
            var best = kinds.OrderBy(d => (double)g.NextCost(d) / d.basePowerWatts).FirstOrDefault();
            var target = UI.ResearchSummaryView.Next(gm, g.EraNumber);
            var ups = gm.UpgradeCatalogue.Where(u => !u.isResearch && u.minEra == g.EraNumber && gm.Upgrades.IsAvailable(u) &&
                                                     (u.effect != UpgradeEffect.AddResearchRate ||
                                                      target != null && gm.Ledger.ResearchPoints < gm.Upgrades.NextCostResearch(target)))
                                         .OrderBy(u => gm.Upgrades.NextCostPennies(u)).ToList();
            decimal unitCost = best != null ? g.NextCost(best) : decimal.MaxValue;
            var up = ups.FirstOrDefault();
            if (up != null && (decimal)gm.Upgrades.NextCostPennies(up) <= unitCost / 4 && gm.Upgrades.Purchase(up)) { _last = up.displayName; return true; }
            if (online < peak * 1.1)
            {
                if (best != null && g.CanBuy(best)) { var u = g.BuyUnit(best); g.EndSync(u, Simulation.Synchroscope.Close.Clean); _last = best.displayName; return true; }
                return false;
            }
            var next = g.Areas.Where(a => !a.Connected && (!(g is Era5DispatchController d5) || d5.Spec(a.Id).Parent == null ||
                                                           d5.Area(d5.Spec(a.Id).Parent).Connected))
                              .OrderBy(a => a.BaseWatts).FirstOrDefault();
            if (next != null && online > (peak + next.BaseWatts * 1.3) * 1.1)
            {
                bool done = g is Era4StationController e4 ? e4.Connect(next.Id) : ((Era5DispatchController)g).Connect(next.Id);
                if (done) { _last = "connect " + g.AreaName(next.Id); return true; }
                return false;
            }
            // Not room for the next feeder yet: build towards it.
            if (next != null && best != null && g.CanBuy(best) && (up == null || (decimal)gm.Upgrades.NextCostPennies(up) > unitCost))
            { var u = g.BuyUnit(best); g.EndSync(u, Simulation.Synchroscope.Close.Clean); _last = best.displayName; return true; }
            if (up != null && gm.Upgrades.Purchase(up)) { _last = up.displayName; return true; }
            if (best != null && g.CanBuy(best) && next == null) { var u = g.BuyUnit(best); g.EndSync(u, Simulation.Synchroscope.Close.Clean); _last = best.displayName; return true; }
            return false;
        }

        private IEnumerator Play(int era, double inherited)
        {
            _log.Clear();
            Save.SaveManager.Instance.StartAtEra(era);
            yield return null;
            if (inherited >= 0) Simulation.SimulationTick.Instance.Grid.InheritedWatts = inherited;
            Time.timeScale = 0f;

            int seconds = 0, lastBuy = 0, longestWait = 0, firstHourWait = 0, buys = 0;
            var sim = Simulation.SimulationTick.Instance;
            var checkpoints = new System.Text.StringBuilder();
            double nextCheckpoint = 0.1 * (era == 2 ? Data.Era2Data.MaxWatts : era == 3 ? Data.Era3Data.MaxWatts
                                         : era == 4 ? Data.Era4Data.MaxWatts : Data.Era5Data.MaxWatts);
            _blackouts = _brownouts = 0;
            _done = false;
            while (Managers.EraManager.Instance.Era == era && seconds < MaxRealSeconds && !_done)
            {
                sim.CatchUpOfflineAsIfHere(1);
                seconds++;
                if (era >= 4 && seconds <= 240 && seconds % 4 == 0)
                {
                    var ug = Object.FindAnyObjectByType<UnitGridController>(); // the current era's
                    foreach (var c in Object.FindObjectsByType<UnitGridController>(FindObjectsSortMode.None)) if (c.IsCurrentEra) ug = c;
                    _log.Append($"\n  t{seconds} f {ug.Frequency:0.000} gen {ug.Grid.Generation / 1e6:0.0} load {ug.Grid.Load / 1e6:0.0} cap {ug.Grid.OnlineCapacity / 1e6:0.0} " +
                                string.Join(",", ug.Units.Select(u => $"{u.Number}:{u.State}:{u.OutputWatts / 1e6:0.0}")) +
                                $" contracts {GM.Contracts.DeliveredWatts / 1e6:0.0}");
                }
                _last = null;
                if (Act(era)) { int wait = seconds - lastBuy; longestWait = Mathf.Max(longestWait, wait);
                                if (seconds < 3600) firstHourWait = Mathf.Max(firstHourWait, wait);
                                lastBuy = seconds; buys++;
                                _log.Append($"\n  {seconds / 60}m {_last} (waited {wait / 60.0:0.0}m, {Utils.NumberFormatter.FormatWatts(sim.Grid.TotalOutputWatts)})"); }
                double output = sim.Grid.TotalOutputWatts;
                if (output >= nextCheckpoint)
                {
                    checkpoints.Append($" {Utils.NumberFormatter.FormatWatts(nextCheckpoint)}@{seconds / 60}m");
                    nextCheckpoint *= 2;
                }
                if (seconds % 3600 == 0) yield return null;
            }
            var works = Object.FindObjectsByType<GridWorksController>(FindObjectsSortMode.None).FirstOrDefault(w => w.EraNumber == era);
            if (works != null)
                _log.Append($"\n  END best delivered {Utils.NumberFormatter.FormatWatts(works.BestDeliveredWatts)} of {Utils.NumberFormatter.FormatWatts(works.GoalWatts)}; " +
                            $"unlit: {string.Join(", ", works.Areas.Where(a => !a.Connected).Select(a => a.Id))}; shed: {string.Join(", ", works.Areas.Where(a => a.Shed).Select(a => a.Id))}");
            Debug.Log($"[Pacing] Era {era} (inherited {inherited}): {(seconds / 3600.0):0.00} h, {buys} purchases, " +
                      $"longest wait {longestWait / 60.0:0.0} min, first-hour longest wait {firstHourWait / 60.0:0.0} min." +
                      $" Blackouts {_blackouts}, brownout seconds {_brownouts}. Output:{checkpoints}{_log}");
            Assert.Pass();
        }

        private static readonly System.Text.StringBuilder _log = new();
        private static string _last;

        // One bot step: book what can be supplied, take the capstone, then buy the cheapest
        // useful thing. Returns whether anything was bought.
        private static bool Act(int era)
        {
            var gm = GM;
            ContractDesk desk = era == 2 ? Object.FindAnyObjectByType<Era2WorksController>()
                              : era == 3 ? Object.FindAnyObjectByType<Era3TownController>()
                              : era == 4 ? Object.FindAnyObjectByType<Era4StationController>()
                                         : (ContractDesk)Object.FindAnyObjectByType<Era5DispatchController>();
            foreach (var def in desk.Contracts.OrderByDescending(ContractDesk.Pay))
                if (desk.HasFreeSlot && desk.IsOffered(def) && desk.SpareForContracts >= def.requiredWatts) desk.Book(def);

            if (era == 2)
            {
                var works = (Era2WorksController)desk;
                if (works.HasCoalEngines && works.Hopper.Fraction(works.CoalBurnCapacityBasis) < 0.3f) works.OrderCoal();
            }
            if (desk.CapstoneReady) { _done = true; desk.TakeCapstone(); return false; }

            // Research first: it costs no money.
            foreach (var def in gm.UpgradeCatalogue.Where(u => u.isResearch && u.minEra == era))
                if (gm.Upgrades.IsAvailable(def) && gm.Upgrades.Purchase(def)) { _last = "learn " + def.displayName; return true; }

            if (desk is UnitGridController unitGrid) return ActGrid(unitGrid);

            var gens = gm.GeneratorCatalogue.Where(g => g.minEra == era && Generators.IsUnlocked(g)).ToList();
            var bestGen = gens.OrderBy(g => (double)Generators.NextCostPennies(g) / g.basePowerWatts).FirstOrDefault();
            decimal genCost = bestGen != null ? Generators.NextCostPennies(bestGen) : decimal.MaxValue;
            // An R&D building only while there's research left to make, and not ahead of need.
            var target = UI.ResearchSummaryView.Next(gm, era);
            var ups = gm.UpgradeCatalogue.Where(u => !u.isResearch && u.minEra == era && gm.Upgrades.IsAvailable(u) &&
                                                     (u.effect != UpgradeEffect.AddResearchRate ||
                                                      target != null && gm.Ledger.ResearchPoints < gm.Upgrades.NextCostResearch(target)))
                                         .OrderBy(u => gm.Upgrades.NextCostPennies(u)).ToList();
            var cheapestUp = ups.FirstOrDefault();
            decimal upCost = cheapestUp != null ? (decimal)gm.Upgrades.NextCostPennies(cheapestUp) : decimal.MaxValue;

            if (era == 3)
            {
                var town = (Era3TownController)desk;
                double capacity = Simulation.SimulationTick.Instance.Grid.TotalOutputWatts;
                double demand = town.Areas.Where(a => a.Connected).Sum(a => a.BaseWatts) * 1.3;
                var next = town.Areas.Where(a => town.StateOf(a.Id) == Era3TownController.ConnectState.Available)
                                     .OrderBy(a => town.ConnectCost(a.Id)).FirstOrDefault();
                // Short of machines for the lit town: build (cheap upgrades may still go first).
                if (capacity < demand)
                {
                    if (cheapestUp != null && upCost <= genCost / 4 && gm.Upgrades.Purchase(cheapestUp))
                    { _last = cheapestUp.displayName; return true; }
                    if (bestGen != null && gm.Ledger.Pennies >= genCost && Generators.TryBuy(bestGen))
                    { _last = bestGen.displayName; return true; }
                    return false;
                }
                // Room to spare: light the next district.
                if (next != null && town.Connect(next.Id)) { _last = "light " + town.Spec(next.Id).Name; return true; }
                var flicker = town.WorstFlicker;
                if (flicker != null && town.CanUpgradeCopper(flicker.Id) &&
                    gm.Ledger.Pennies >= town.CopperCost(flicker.Id) * 2 && town.UpgradeCopper(flicker.Id))
                { _last = "copper " + town.Spec(flicker.Id).Name; return true; }
                if (next != null) return false; // saving for it
            }

            if (cheapestUp != null && upCost <= genCost * 3 && gm.Upgrades.Purchase(cheapestUp))
            { _last = cheapestUp.displayName; return true; }
            if (bestGen != null && gm.Ledger.Pennies >= genCost && Generators.TryBuy(bestGen))
            { _last = bestGen.displayName; return true; }
            return false;
        }
    }
}
