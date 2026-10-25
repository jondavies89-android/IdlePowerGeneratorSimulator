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
            double nextCheckpoint = 0.1 * (era == 2 ? Data.Era2Data.MaxWatts : Data.Era3Data.MaxWatts);
            while (Managers.EraManager.Instance.Era == era && seconds < MaxRealSeconds)
            {
                sim.CatchUpOffline(1);
                seconds++;
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
            Debug.Log($"[Pacing] Era {era} (inherited {inherited}): {(seconds / 3600.0):0.00} h, {buys} purchases, " +
                      $"longest wait {longestWait / 60.0:0.0} min, first-hour longest wait {firstHourWait / 60.0:0.0} min." +
                      $" Output:{checkpoints}{_log}");
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
                                         : (ContractDesk)Object.FindAnyObjectByType<Era3TownController>();
            foreach (var def in desk.Contracts.OrderByDescending(ContractDesk.Pay))
                if (desk.HasFreeSlot && desk.IsOffered(def) && desk.SpareForContracts >= def.requiredWatts) desk.Book(def);

            if (era == 2)
            {
                var works = (Era2WorksController)desk;
                if (works.HasCoalEngines && works.Hopper.Fraction(works.CoalBurnCapacityBasis) < 0.3f) works.OrderCoal();
            }
            if (desk is GridWorksController grid && grid.CapstoneReady) { grid.TakeCapstone(); return false; }

            // Research first: it costs no money.
            foreach (var def in gm.UpgradeCatalogue.Where(u => u.isResearch && u.minEra == era))
                if (gm.Upgrades.IsAvailable(def) && gm.Upgrades.Purchase(def)) { _last = "learn " + def.displayName; return true; }

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
