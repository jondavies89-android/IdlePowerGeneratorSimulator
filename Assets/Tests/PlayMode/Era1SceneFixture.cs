using System.Collections;
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
    }
}
