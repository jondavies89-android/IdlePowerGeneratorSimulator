using System;
using System.Collections.Generic;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Models;
using UnityEditor;
using UnityEngine;

// Creates or updates the Era 3 ScriptableObject assets from Era3Data, as the Era 1 and 2
// builders do. Prices are written in pennies (Era3Data gives pounds; 240 d = £1).
public static class Era3AssetBuilder
{
    private const string EraFolder       = "Assets/ScriptableObjects/Eras";
    private const string UpgradeFolder   = "Assets/ScriptableObjects/Upgrades/Era3";
    private const string GeneratorFolder = "Assets/ScriptableObjects/Generators/Era3";
    private const string ContractFolder  = "Assets/ScriptableObjects/Contracts/Era3";

    public const string EraConfigPath = EraFolder + "/Era3Config.asset";

    private const double PenniesPerPound = 240;

    [MenuItem("IdlePowerGen/Build/Era 3 Assets")]
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
        Debug.Log("[Era3AssetBuilder] Era 3 assets written");
    }

    private static void BuildEraConfig()
    {
        var cfg = LoadOrCreate<EraConfig>(EraConfigPath);
        cfg.eraNumber         = Era3Data.EraNumber;
        cfg.eraName           = "The War of Currents";
        cfg.flavorYear        = "1893";
        cfg.flavorLocation    = "Westgate";
        cfg.minWatts          = Era3Data.MinWatts;
        cfg.maxWatts          = Era3Data.MaxWatts;
        cfg.useJoules         = false;
        cfg.useCash           = true;
        cfg.useReputation     = true;
        cfg.contractBaseRate  = Era3Data.ContractBaseRate;
        cfg.streetRateFactor  = 0f; // no street: spare power isn't made
        cfg.tariffPenniesPerWattHour = Era3Data.TariffPenniesPerWattHour;
        cfg.upgradeBranchNames  = Era3Data.BranchNames.ToArray();
        cfg.researchBranchNames = Era3Data.ResearchBranchNames.ToArray();
        cfg.offlineEraFactor  = Era3Data.OfflineEraFactor;
        cfg.offlineTauHours   = Era3Data.OfflineTauHours;
        cfg.offlineFlavorText = Era3Data.OfflineFlavorText;
        EditorUtility.SetDirty(cfg);
    }

    private static void BuildGenerators()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era3Data.Generators)
        {
            string path = $"{GeneratorFolder}/Generator_{s.Type}.asset";
            keep.Add(path);
            var def = LoadOrCreate<GeneratorDefinition>(path);
            def.generatorType            = s.Type;
            def.displayName              = s.Name;
            def.description              = s.Description;
            def.minEra                   = Era3Data.EraNumber;
            def.basePowerWatts           = s.Watts;
            def.fuelConsumptionPerSecond = 0;
            def.wagePenniesPerHour       = 0;
            def.coalPenniesPerHour       = 0;
            def.baseCostJoules           = 0;
            def.baseCostPennies          = s.CostPounds * PenniesPerPound;
            def.requiredUpgradeId        = s.UnlockedBy;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<GeneratorDefinition>(GeneratorFolder, keep);
    }

    private static void BuildContracts()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era3Data.Contracts)
        {
            string path = $"{ContractFolder}/Contract_{s.Id}.asset";
            keep.Add(path);
            var def = LoadOrCreate<ContractDefinition>(path);
            def.contractId          = s.Id;
            def.clientName          = s.Client;
            def.description         = s.Description;
            def.tier                = s.Tier;
            def.minEra              = Era3Data.EraNumber;
            def.requiredWatts       = s.Watts;
            def.durationHours       = s.Hours;
            def.mustHoldOutput      = false;
            def.requiredReputation  = s.RequiredReputation;
            def.payRateMultiplier   = s.PayMultiplier;
            def.reputationOnSuccess = s.ReputationOnSuccess;
            def.reputationOnFail    = -2f;
            def.failThreshold       = 0.5f;
            def.shortfallLeeway               = Era3Data.ShortfallLeeway;
            def.shortfallCountdownMinutes     = Era3Data.ShortfallCountdownMinutes;
            def.shortfallForgivenAfterMinutes = Era3Data.ShortfallForgivenAfterMinutes;
            EditorUtility.SetDirty(def);
        }
        DeleteStale<ContractDefinition>(ContractFolder, keep);
    }

    private static void BuildUpgrades()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era3Data.Upgrades)
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
        foreach (var s in Era3Data.Research)
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
    }

    private static UpgradeDefinition Upgrade(string id, HashSet<string> keep)
    {
        string path = $"{UpgradeFolder}/Upgrade_{id}.asset";
        keep.Add(path);
        var def = LoadOrCreate<UpgradeDefinition>(path);
        def.upgradeId      = id;
        def.minEra         = Era3Data.EraNumber;
        def.costJoules     = 0;
        def.secondaryValue = 0;
        return def;
    }

    // ---- Catalogue queries used by the scene builder ----

    public static UpgradeDefinition[] Upgrades() =>
        Era1AssetBuilder.LoadAll<UpgradeDefinition>(UpgradeFolder)
            .OrderBy(u => u.costPennies + u.costResearch).ToArray();

    public static GeneratorDefinition[] Generators() =>
        Era1AssetBuilder.LoadAll<GeneratorDefinition>(GeneratorFolder).OrderBy(g => g.baseCostPennies).ToArray();

    public static ContractDefinition[] Contracts() =>
        Era1AssetBuilder.LoadAll<ContractDefinition>(ContractFolder).OrderBy(c => c.requiredWatts).ToArray();

    // ---- Helpers ----

    internal static void DeleteStale<T>(string folder, HashSet<string> keep) where T : ScriptableObject
    {
        foreach (var stale in Era1AssetBuilder.LoadAll<T>(folder)
                     .Select(AssetDatabase.GetAssetPath).Where(p => !keep.Contains(p)).ToList())
            AssetDatabase.DeleteAsset(stale);
    }

    internal static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }
}
