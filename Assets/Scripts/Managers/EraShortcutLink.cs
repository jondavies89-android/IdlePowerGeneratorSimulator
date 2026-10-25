using System.Collections;
using UnityEngine;

namespace IdlePowerGen.Managers
{
    // Play-testing shortcut: opening the game with ?era=N (2 to the last built era) starts a
    // fresh run at the start of that era, replacing any save, as the DEV strip's era buttons
    // do. The query is then cleared from the address bar, so a refresh carries on with that
    // run instead of wiping it again.
    public class EraShortcutLink : MonoBehaviour
    {
        public const string QueryName = "era";

        private IEnumerator Start()
        {
            // GameManager.Start loads the save; the fresh run has to land after it.
            yield return null;

            if (!int.TryParse(WebPage.QueryValue(QueryName), out int era)) yield break;
            if (era < 2 || era > EraManager.LastBuiltEra) yield break;
            WebPage.ClearQuery();
            Save.SaveManager.Instance?.StartAtEra(era);
        }
    }
}
