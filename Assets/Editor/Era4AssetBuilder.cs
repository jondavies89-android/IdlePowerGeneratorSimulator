using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Models;
using UnityEditor;
using UnityEngine;
using static Era3AssetBuilder;

// Creates or updates the Era 4 ScriptableObject assets from Era4Data. Prices in pennies.
public static class Era4AssetBuilder
{
    private const string EraFolder       = "Assets/ScriptableObjects/Eras";
    private const string UpgradeFolder   = "Assets/ScriptableObjects/Upgrades/Era4";
    private const string GeneratorFolder = "Assets/ScriptableObjects/Generators/Era4";
    private const string ContractFolder  = "Assets/ScriptableObjects/Contracts/Era4";

    public const string EraConfigPath = EraFolder + "/Era4Config.asset";
    private const double PenniesPerPound = 240;

    [MenuItem("IdlePowerGen/Build/Era 4 Assets")]
    public static void Build()
    {
        Era1AssetBuilder.EnsureFolder(EraFolder);
        Era1AssetBuilder.EnsureFolder(UpgradeFolder);
        Era1AssetBuilder.EnsureFolder(GeneratorFolder);
        Era1AssetBuilder.EnsureFolder(ContractFolder);

        var cfg = LoadOrCreate<EraConfig>(EraConfigPath);
        cfg.eraNumber         = Era4Data.EraNumber;
        cfg.eraName           = "The Grid Awakens";
        cfg.flavorYear        = "1928";
        cfg.flavorLocation    = "Central Electric Station";
        cfg.minWatts          = Era4Data.MinWatts;
        cfg.maxWatts          = Era4Data.MaxWatts;
        cfg.useJoules         = false;
        cfg.useCash           = true;
        cfg.useReputation     = true;
        cfg.contractBaseRate  = Era4Data.ContractBaseRate;
        cfg.streetRateFactor  = 0f;
        cfg.tariffPenniesPerWattHour = Era4Data.TariffPenniesPerWattHour;
        cfg.upgradeBranchNames  = Era4Data.BranchNames.ToArray();
        cfg.researchBranchNames = Era4Data.ResearchBranchNames.ToArray();
        cfg.offlineEraFactor  = Era4Data.OfflineEraFactor;
        cfg.offlineTauHours   = Era4Data.OfflineTauHours;
        cfg.offlineFlavorText = Era4Data.OfflineFlavorText;
        EditorUtility.SetDirty(cfg);

        var keep = new HashSet<string>();
        foreach (var s in Era4Data.Units)
        {
            string path = $"{GeneratorFolder}/Generator_{s.Type}.asset";
            keep.Add(path);
            var def = LoadOrCreate<GeneratorDefinition>(path);
            def.generatorType            = s.Type;
            def.displayName              = s.Name;
            def.description              = s.Description;
            def.minEra                   = Era4Data.EraNumber;
            def.basePowerWatts           = s.Watts;
            def.fuelConsumptionPerSecond = 0;
            def.wagePenniesPerHour       = 0;
            def.coalPenniesPerHour       = 0;
            def.baseCostJoules           = 0;
            def.baseCostPennies          = s.CostPounds * PenniesPerPound;
            def.requiredUpgradeId        = s.UnlockedBy;
            def.governorSeconds          = s.GovernorSeconds;
            def.inertiaSeconds           = s.InertiaSeconds;
            def.coalTonnesPerMWh         = s.CoalTonnesPerMWh;
            def.serviceIntervalHours     = s.ServiceIntervalHours;
            def.blackStartCapable        = s.BlackStart;
            def.maxUnits                 = s.MaxUnits;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<GeneratorDefinition>(GeneratorFolder, keep);

        keep.Clear();
        foreach (var s in Era4Data.Contracts)
        {
            string path = $"{ContractFolder}/Contract_{s.Id}.asset";
            keep.Add(path);
            var def = LoadOrCreate<ContractDefinition>(path);
            def.contractId          = s.Id;
            def.clientName          = s.Client;
            def.description         = s.Description;
            def.tier                = s.Tier;
            def.minEra              = Era4Data.EraNumber;
            def.requiredWatts       = s.Watts;
            def.durationHours       = s.Hours;
            def.mustHoldOutput      = false;
            def.availableStartHour  = s.FromHour;
            def.availableEndHour    = s.ToHour;
            def.requiredReputation  = s.RequiredReputation;
            def.payRateMultiplier   = s.PayMultiplier;
            def.reputationOnSuccess = s.ReputationOnSuccess;
            def.reputationOnFail    = -2f;
            def.failThreshold       = 0.5f;
            def.shortfallLeeway               = Era4Data.ShortfallLeeway;
            def.shortfallCountdownMinutes     = Era4Data.ShortfallCountdownMinutes;
            def.shortfallForgivenAfterMinutes = Era4Data.ShortfallForgivenAfterMinutes;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<ContractDefinition>(ContractFolder, keep);

        keep.Clear();
        foreach (var s in Era4Data.Upgrades)
        {
            var def = Upgrade(s.Id, keep);
            def.displayName        = s.Name;
            def.description        = s.Description;
            def.costPennies        = s.CostPounds * PenniesPerPound;
            def.costResearch       = 0;
            def.isResearch         = false;
            def.costGrowth         = s.CostGrowth;
            def.maxLevel           = s.MaxLevel;
            def.requiredReputation = s.RequiredReputation;
            def.effect             = s.Effect;
            def.effectValue        = s.Value;
            def.unlocksId          = s.UnlocksId;
            def.requiredUpgradeIds = s.Prerequisites?.ToArray() ?? Array.Empty<string>();
            def.treeBranch         = s.Branch;
            def.treeRow            = s.Row;
            EditorUtility.SetDirty(def);
        }
        foreach (var s in Era4Data.Research)
        {
            var def = Upgrade(s.Id, keep);
            def.displayName        = s.Name;
            def.description        = s.Description;
            def.costPennies        = 0;
            def.costResearch       = s.CostRP;
            def.isResearch         = true;
            def.costGrowth         = 1;
            def.maxLevel           = 1;
            def.requiredReputation = 0;
            def.effect             = s.Effect;
            def.effectValue        = 0;
            def.unlocksId          = s.UnlocksId;
            def.requiredUpgradeIds = s.Prerequisites?.ToArray() ?? Array.Empty<string>();
            def.treeBranch         = s.Branch;
            def.treeRow            = s.Row;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<UpgradeDefinition>(UpgradeFolder, keep);

        AssetDatabase.SaveAssets();
        Debug.Log("[Era4AssetBuilder] Era 4 assets written");
    }

    private static UpgradeDefinition Upgrade(string id, HashSet<string> keep)
    {
        string path = $"{UpgradeFolder}/Upgrade_{id}.asset";
        keep.Add(path);
        var def = LoadOrCreate<UpgradeDefinition>(path);
        def.upgradeId      = id;
        def.minEra         = Era4Data.EraNumber;
        def.costJoules     = 0;
        def.secondaryValue = 0;
        return def;
    }

    public static UpgradeDefinition[] Upgrades() =>
        Era1AssetBuilder.LoadAll<UpgradeDefinition>(UpgradeFolder).OrderBy(u => u.costPennies + u.costResearch).ToArray();

    public static GeneratorDefinition[] Generators() =>
        Era1AssetBuilder.LoadAll<GeneratorDefinition>(GeneratorFolder).OrderBy(g => g.baseCostPennies).ToArray();

    public static ContractDefinition[] Contracts() =>
        Era1AssetBuilder.LoadAll<ContractDefinition>(ContractFolder).OrderBy(c => c.requiredWatts).ToArray();
}
