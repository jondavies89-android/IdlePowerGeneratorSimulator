using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace IdlePowerGen.Tests
{
    // Loads the real Era 1 scene on a scratch save, from a fresh start, for each test.
    // Any Debug.LogError or exception during a test fails it (Unity Test Framework default).
    public abstract class Era1SceneFixture
    {
        protected const string SceneName = "Era1_Workshop";

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Save.SaveManager.SaveFileName = "save_playmode_tests.json";
            SceneManager.LoadScene(SceneName);
            yield return null; // Awake/OnEnable
            yield return null; // Start (GameManager loads the save here)
            Save.SaveManager.Instance.ResetProgress();
            yield return null;
        }

        [TearDown]
        public void RestoreSaveName()
        {
            Save.SaveManager.Instance?.ResetProgress();
            Save.SaveManager.SaveFileName = "save.json";
        }

        // Owns these upgrade levels, as a loaded save would, with their effects applied.
        protected static void SetLevels(params (string id, int level)[] levels)
        {
            var dict = new Dictionary<string, int>();
            foreach (var (id, level) in levels) dict[id] = level;
            Managers.GameManager.Instance.Upgrades.LoadFromSave(dict);
            Utils.EventBus.Publish(new GameLoadedEvent());
        }

        protected static Models.UpgradeDefinition Upgrade(string id)
        {
            foreach (var u in Managers.GameManager.Instance.UpgradeCatalogue)
                if (u.upgradeId == id) return u;
            throw new System.ArgumentException(id);
        }

        protected static Simulation.Era1Flywheel Flywheel => Simulation.SimulationTick.Instance.Flywheel;
    }
}
