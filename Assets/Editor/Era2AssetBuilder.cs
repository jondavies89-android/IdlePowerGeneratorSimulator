using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Models;
using UnityEditor;
using UnityEngine;

// Creates or updates the Era 2 ScriptableObject assets from Era2Data, as Era1AssetBuilder
// does for Era 1: re-running it overwrites Inspector tweaks with Era2Data's numbers.
public static class Era2AssetBuilder
{
    private const string EraFolder       = "Assets/ScriptableObjects/Eras";
    private const string UpgradeFolder   = "Assets/ScriptableObjects/Upgrades/Era2";
    private const string GeneratorFolder = "Assets/ScriptableObjects/Generators/Era2";
    private const string ContractFolder  = "Assets/ScriptableObjects/Contracts/Era2";

    public const string EraConfigPath = EraFolder + "/Era2Config.asset";

    [MenuItem("IdlePowerGen/Build/Era 2 Assets")]
    public static void Build()
    {
        Era1AssetBuilder.EnsureFolder(EraFolder);
        Era1AssetBuilder.EnsureFolder(UpgradeFolder);
        Era1AssetBuilder.EnsureFolder(GeneratorFolder);
        Era1AssetBuilder.EnsureFolder(ContractFolder);

        BuildEraConfig();
        BuildGenerators();
        BuildContracts();
        BuildUpgrades();

        AssetDatabase.SaveAssets();
        Debug.Log("[Era2AssetBuilder] Era 2 assets written");
    }

    private static void BuildEraConfig()
    {
        var cfg = LoadOrCreate<EraConfig>(EraConfigPath);
        cfg.eraNumber         = Era2Data.EraNumber;
        cfg.eraName           = "The Demonstration";
        cfg.flavorYear        = "1851";
        cfg.flavorLocation    = "London";
        cfg.minWatts          = Era2Data.MinWatts;
        cfg.maxWatts          = Era2Data.MaxWatts;
        cfg.useJoules         = true;
        cfg.useCash           = true;
        cfg.useReputation     = true;
        cfg.contractBaseRate  = Era2Data.ContractBaseRate;
        cfg.streetRateFactor  = Era2Data.StreetRateFactor;
        cfg.upgradeBranchNames = Era2Data.BranchNames.ToArray();
        cfg.offlineEraFactor  = Era2Data.OfflineEraFactor;
        cfg.offlineTauHours   = Era2Data.OfflineTauHours;
        cfg.offlineFlavorText = Era2Data.OfflineFlavorText;
        EditorUtility.SetDirty(cfg);
    }

    private static void BuildGenerators()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era2Data.Generators)
        {
            string path = $"{GeneratorFolder}/Generator_{s.Type}.asset";
            keep.Add(path);
            var def = LoadOrCreate<GeneratorDefinition>(path);
            def.generatorType            = s.Type;
            def.displayName              = s.Name;
            def.description              = s.Description;
            def.minEra                   = Era2Data.EraNumber;
            def.basePowerWatts           = s.Watts;
            def.fuelConsumptionPerSecond = 0; // the shared coal hopper feeds them instead
            def.wagePenniesPerHour       = s.WagePerHour;
            def.coalPenniesPerHour       = s.CoalPerHour;
            def.baseCostJoules           = 0;
            def.baseCostPennies          = s.CostPennies;
            def.requiredUpgradeId        = s.UnlockedBy;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<GeneratorDefinition>(GeneratorFolder, keep);
    }

    private static void BuildContracts()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era2Data.Contracts)
        {
            string path = $"{ContractFolder}/Contract_{s.Id}.asset";
            keep.Add(path);
            var def = LoadOrCreate<ContractDefinition>(path);
            def.contractId          = s.Id;
            def.clientName          = s.Client;
            def.description         = s.Description;
            def.tier                = s.Tier;
            def.minEra              = Era2Data.EraNumber;
            def.requiredWatts       = s.Watts;
            def.durationHours       = s.Hours;
            def.mustHoldOutput      = false; // bible §5.1.6: judged on average, failing below 50 %
            def.requiredReputation  = s.RequiredReputation;
            def.payRateMultiplier   = s.PayMultiplier;
            def.reputationOnSuccess = s.ReputationOnSuccess;
            def.reputationOnFail    = -2f;
            def.failThreshold       = 0.5f;
            def.shortfallLeeway               = Era2Data.ShortfallLeeway;
            def.shortfallCountdownMinutes     = Era2Data.ShortfallCountdownMinutes;
            def.shortfallForgivenAfterMinutes = Era2Data.ShortfallForgivenAfterMinutes;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<ContractDefinition>(ContractFolder, keep);
    }

    private static void BuildUpgrades()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era2Data.Upgrades)
        {
            string path = $"{UpgradeFolder}/Upgrade_{s.Id}.asset";
            keep.Add(path);
            var def = LoadOrCreate<UpgradeDefinition>(path);
            def.upgradeId          = s.Id;
            def.displayName        = s.Name;
            def.description        = s.Description;
            def.minEra             = Era2Data.EraNumber;
            def.costJoules         = 0;
            def.costPennies        = s.CostPennies;
            def.costGrowth         = s.CostGrowth;
            def.maxLevel           = s.MaxLevel;
            def.requiredReputation = s.RequiredReputation;
            def.effect             = s.Effect;
            def.effectValue        = s.Value;
            def.secondaryValue     = 0;
            def.unlocksId          = s.UnlocksId;
            def.requiredUpgradeIds = s.Prerequisites?.ToArray() ?? Array.Empty<string>();
            def.treeBranch         = s.Branch;
            def.treeRow            = s.Row;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<UpgradeDefinition>(UpgradeFolder, keep);
    }

    // ---- Catalogue queries used by the scene builder ----

    public static UpgradeDefinition[] Upgrades() =>
        Era1AssetBuilder.LoadAll<UpgradeDefinition>(UpgradeFolder).OrderBy(u => u.costPennies).ToArray();

    public static GeneratorDefinition[] Generators() =>
        Era1AssetBuilder.LoadAll<GeneratorDefinition>(GeneratorFolder).OrderBy(g => g.baseCostPennies).ToArray();

    public static ContractDefinition[] Contracts() =>
        Era1AssetBuilder.LoadAll<ContractDefinition>(ContractFolder).OrderBy(c => c.requiredWatts).ToArray();

    // ---- Helpers ----

    private static void DeleteStale<T>(string folder, HashSet<string> keep) where T : ScriptableObject
    {
        foreach (var stale in Era1AssetBuilder.LoadAll<T>(folder)
                     .Select(AssetDatabase.GetAssetPath).Where(p => !keep.Contains(p)).ToList())
            AssetDatabase.DeleteAsset(stale);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
