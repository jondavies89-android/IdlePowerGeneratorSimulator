using System.Collections;
using UnityEngine;

namespace IdlePowerGen.Managers
{
    // Play-testing shortcut: opening the game with ?era=2 starts a fresh run at the start of
    // Era 2, replacing any save. The query is then cleared from the address bar, so a refresh
    // carries on with that run instead of wiping it again.
    public class EraShortcutLink : MonoBehaviour
    {
        public const string QueryName = "era";

        private IEnumerator Start()
        {
            // GameManager.Start loads the save; the fresh run has to land after it.
            yield return null;

            if (WebPage.QueryValue(QueryName) != "2") yield break;
            WebPage.ClearQuery();
            Save.SaveManager.Instance?.StartAtEra2();
        }
    }
}
