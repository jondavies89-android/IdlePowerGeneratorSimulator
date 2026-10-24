using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdlePowerGen.Data;
using IdlePowerGen.Models;
using UnityEditor;
using UnityEngine;

// Creates or updates the Era 1 ScriptableObject assets from Era1Data (todo §4).
// Era1Data seeds the numbers; once an asset exists a designer may tune it in the
// Inspector, and re-running this builder overwrites those tweaks — so after the first
// run, treat the assets as the source of truth and stop running it.
public static class Era1AssetBuilder
{
    private const string EraFolder       = "Assets/ScriptableObjects/Eras";
    private const string UpgradeFolder   = "Assets/ScriptableObjects/Upgrades/Era1";
    private const string GeneratorFolder = "Assets/ScriptableObjects/Generators/Era1";
    private const string ContractFolder  = "Assets/ScriptableObjects/Contracts/Era1";

    public const string EraConfigPath = EraFolder + "/Era1Config.asset";

    // Asset file names, keyed by upgrade id.
    private static readonly Dictionary<string, string> UpgradeAssetNames = new()
    {
        [Era1Data.BetterCrankId]  = "Upgrade_BetterCrank",
        [Era1Data.IronFlywheelId] = "Upgrade_IronFlywheel",
        [Era1Data.BearingsId]     = "Upgrade_BrassBearings",
        [Era1Data.MagnetsId]      = "Upgrade_LodestoneMagnets",
        [Era1Data.WaterWheelId]   = "Upgrade_WaterWheel",
        [Era1Data.BeltsGearingId] = "Upgrade_BeltsAndGearing",
    };

    // Player-facing copy. Plain English, period voice; the number is on the card already.
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        [Era1Data.BetterCrankId]  = "A longer handle and a surer grip.",
        [Era1Data.IronFlywheelId] = "A heavy iron wheel keeps turning after your hand stops.",
        [Era1Data.BearingsId]     = "Oiled brass where there was dry wood. The wheel runs longer.",
        [Era1Data.MagnetsId]      = "Stronger lodestones double the current, and drag on the wheel.",
        [Era1Data.WaterWheelId]   = "There is a river behind the workshop. A wheel would turn all day.",
        [Era1Data.BeltsGearingId] = "Better belts and gearing pass more of the river to the dynamo.",
    };

    [MenuItem("IdlePowerGen/Build/Era 1 Assets")]
    public static void Build()
    {
        EnsureFolder(EraFolder);
        EnsureFolder(UpgradeFolder);

        BuildEraConfig();
        BuildUpgrades();
        BuildContracts();

        // Era 1 no longer has stackable machines; the water wheel is an upgrade.
        if (AssetDatabase.IsValidFolder(GeneratorFolder)) AssetDatabase.DeleteAsset(GeneratorFolder);

        AssetDatabase.SaveAssets();
        Debug.Log("[Era1AssetBuilder] Era 1 assets written");
    }

    private static void BuildEraConfig()
    {
        var cfg = LoadOrCreate<EraConfig>(EraConfigPath);
        cfg.eraNumber         = Era1Data.EraNumber;
        cfg.eraName           = "The Workshop";
        cfg.flavorYear        = "1821";
        cfg.flavorLocation    = "London";
        cfg.minWatts          = Era1Data.MinWatts;
        cfg.maxWatts          = Era1Data.MaxWatts;
        cfg.useJoules         = true;
        cfg.useCash           = false;
        cfg.useReputation     = false;
        cfg.offlineEraFactor  = 1.0f;
        cfg.offlineTauHours   = 4f;
        cfg.offlineFlavorText = Era1Data.OfflineFlavorText;
        EditorUtility.SetDirty(cfg);
    }

    private static void BuildUpgrades()
    {
        var keep = new HashSet<string>();
        foreach (var s in Era1Data.Upgrades)
        {
            string path = $"{UpgradeFolder}/{UpgradeAssetNames[s.Id]}.asset";
            keep.Add(path);

            var def = LoadOrCreate<UpgradeDefinition>(path);
            def.upgradeId          = s.Id;
            def.displayName        = s.Name;
            def.description        = Descriptions[s.Id];
            def.minEra             = 1;
            def.costJoules         = s.CostJoules;
            def.costPennies        = 0;
            def.costGrowth         = s.CostGrowth;
            def.maxLevel           = s.MaxLevel;
            def.effect             = s.Effect;
            def.effectValue        = s.Value;
            def.secondaryValue     = s.SecondaryValue;
            def.unlocksId          = null;
            def.requiredUpgradeIds = string.IsNullOrEmpty(s.PrerequisiteId)
                                     ? Array.Empty<string>() : new[] { s.PrerequisiteId };
            EditorUtility.SetDirty(def);
        }

        // An upgrade dropped from Era1Data must not linger in the catalogue.
        foreach (var stale in LoadAll<UpgradeDefinition>(UpgradeFolder)
                     .Select(AssetDatabase.GetAssetPath).Where(p => !keep.Contains(p)).ToList())
            AssetDatabase.DeleteAsset(stale);
    }

    private static void BuildContracts()
    {
        EnsureFolder(ContractFolder);
        var def = LoadOrCreate<ContractDefinition>($"{ContractFolder}/Contract_FirstCustomer.asset");
        def.contractId          = Era1Data.FirstCustomerContractId;
        def.clientName          = "Mr Hale, next door";
        def.description         = "He wants his workshop lit through the evening. He'll pay a shilling.";
        def.tier                = ContractTier.Standard;
        def.minEra              = 1;
        def.requiredWatts       = Era1Data.MaxWatts;
        def.durationHours       = Era1Data.FirstCustomerHours;
        def.mustHoldOutput      = true; // "1 kW sustained" (bible §5.1.3), not 1 kW on average
        def.availableStartHour  = 0f;
        def.availableEndHour    = 24f;
        def.payRateMultiplier   = Era1Data.FirstCustomerPayMult;
        def.reputationOnSuccess = 0f; // reputation doesn't exist until Era 2
        def.reputationOnFail    = 0f;
        def.failThreshold       = 0.5f;
        EditorUtility.SetDirty(def);
    }

    // ---- Catalogue queries used by the scene builder ----

    public static T[] LoadAll<T>(string folder) where T : ScriptableObject =>
        AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder })
            .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
            .ToArray();

    // Cheapest first, which is also the order the player meets them in.
    public static UpgradeDefinition[] Upgrades() =>
        LoadAll<UpgradeDefinition>(UpgradeFolder).OrderBy(u => u.costJoules).ToArray();

    public static GeneratorDefinition[] Generators() =>
        AssetDatabase.IsValidFolder(GeneratorFolder)
            ? LoadAll<GeneratorDefinition>(GeneratorFolder).OrderBy(g => g.baseCostJoules).ToArray()
            : Array.Empty<GeneratorDefinition>();

    public static EraConfig[] EraConfigs() => LoadAll<EraConfig>(EraFolder);

    public static ContractDefinition[] Contracts() => LoadAll<ContractDefinition>(ContractFolder);

    // ---- Helpers ----

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
