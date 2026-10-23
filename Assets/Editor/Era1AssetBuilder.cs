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

    public const string EraConfigPath = EraFolder + "/Era1Config.asset";

    // Asset file names from the todo tables, keyed by upgrade id.
    private static readonly Dictionary<string, string> UpgradeAssetNames = new()
    {
        ["era1_better_crank"]          = "Upgrade_BetterCrank",
        ["era1_iron_flywheel"]         = "Upgrade_IronFlywheel",
        ["era1_gear_train"]            = "Upgrade_GearTrain",
        ["era1_belt_drive"]            = "Upgrade_LeatherBeltDrive",
        ["era1_cam_mechanism"]         = "Upgrade_CamMechanism",
        ["era1_unlock_spinning_jenny"] = "Upgrade_UnlockSpinningJenny",
        ["era1_unlock_waterwheel"]     = "Upgrade_UnlockWaterwheel",
        ["era1_unlock_steam_boiler"]   = "Upgrade_UnlockSteamBoiler",
        ["era1_foreman"]               = "Upgrade_HireForeman",
    };

    // Player-facing copy. Plain English, period voice; the number is on the card already.
    private static readonly Dictionary<string, string> Descriptions = new()
    {
        ["era1_better_crank"]          = "A longer handle and a surer grip.",
        ["era1_iron_flywheel"]         = "A heavy iron wheel carries each turn further.",
        ["era1_gear_train"]            = "Brass gears spin the magnet faster than any wrist.",
        ["era1_belt_drive"]            = "A leather belt from a foot treadle joins in.",
        ["era1_cam_mechanism"]         = "A cam smooths every push into a steady spin.",
        ["era1_unlock_spinning_jenny"] = "An old spinning jenny could turn the magnet for you.",
        ["era1_unlock_waterwheel"]     = "There is a river behind the workshop. A wheel would turn all day.",
        ["era1_unlock_steam_boiler"]   = "The parish will allow a boiler, if you pay for the licence.",
        ["era1_foreman"]               = "Someone to keep every machine oiled and running.",
        ["SpinningJenny"]              = "Turns the magnet while you rest.",
        ["Waterwheel"]                 = "The river never tires.",
        ["SteamBoiler"]                = "Coal, fire and pressure.",
    };

    [MenuItem("IdlePowerGen/Build/Era 1 Assets")]
    public static void Build()
    {
        EnsureFolder(EraFolder);
        EnsureFolder(UpgradeFolder);
        EnsureFolder(GeneratorFolder);

        BuildEraConfig();
        BuildUpgrades();
        BuildGenerators();

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
        foreach (var s in Era1Data.CrankUpgrades)
            WriteUpgrade(s.Id, s.Name, s.CostJoules, UpgradeEffect.MultiplyCrankTapValue,
                         s.CtvMultiplier, null, s.PrerequisiteId);

        foreach (var s in Era1Data.GeneratorUnlocks)
            WriteUpgrade(s.Id, s.Name, s.CostJoules, UpgradeEffect.UnlockGenerator,
                         0, s.UnlocksGeneratorType, s.PrerequisiteId);

        var f = Era1Data.ForemanUpgrade;
        WriteUpgrade(f.Id, f.Name, f.CostJoules, UpgradeEffect.MultiplyAllOutput,
                     f.Multiplier, null, f.PrerequisiteId);
    }

    private static void WriteUpgrade(string id, string name, double cost, UpgradeEffect effect,
                                     double value, string unlocksId, string prereq)
    {
        var def = LoadOrCreate<UpgradeDefinition>($"{UpgradeFolder}/{UpgradeAssetNames[id]}.asset");
        def.upgradeId          = id;
        def.displayName        = name;
        def.description        = Descriptions[id];
        def.minEra             = 1;
        def.costJoules         = cost;
        def.costPennies        = 0;
        def.effect             = effect;
        def.effectValue        = value;
        def.unlocksId          = unlocksId;
        def.requiredUpgradeIds = string.IsNullOrEmpty(prereq) ? Array.Empty<string>() : new[] { prereq };
        EditorUtility.SetDirty(def);
    }

    private static void BuildGenerators()
    {
        foreach (var s in Era1Data.PassiveGenerators)
        {
            var type = (GeneratorType)Enum.Parse(typeof(GeneratorType), s.Id);
            var def  = LoadOrCreate<GeneratorDefinition>($"{GeneratorFolder}/Generator_{s.Id}.asset");
            def.generatorType            = type;
            def.displayName              = s.Name;
            def.description              = Descriptions[s.Id];
            def.minEra                   = 1;
            def.basePowerWatts           = s.BasePowerWatts;
            def.fuelConsumptionPerSecond = 0; // no fuel mechanic in Era 1
            def.baseCostJoules           = s.BaseJoules;
            def.baseCostPennies          = 0;
            def.requiredUpgradeId        = s.RequiredUpgradeId;
            EditorUtility.SetDirty(def);
        }
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
        LoadAll<GeneratorDefinition>(GeneratorFolder).OrderBy(g => g.baseCostJoules).ToArray();

    public static EraConfig[] EraConfigs() => LoadAll<EraConfig>(EraFolder);

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
