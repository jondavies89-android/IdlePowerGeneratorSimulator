using System;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace IdlePowerGen.Managers
{
    // The one place that knows the game is running in a browser page (CLAUDE.md: platform
    // code in one small adapter). Elsewhere, and in the Editor, there is no query string.
    public static class WebPage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void IpgClearUrlQuery();
#endif

        // The value of ?name=value in the page's address, or null.
        public static string QueryValue(string name)
        {
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return null;
            string query = url.Substring(q + 1);
            int hash = query.IndexOf('#');
            if (hash >= 0) query = query.Substring(0, hash);
            foreach (var pair in query.Split('&'))
            {
                int eq = pair.IndexOf('=');
                string key = eq >= 0 ? pair.Substring(0, eq) : pair;
                if (string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
                    return eq >= 0 ? Uri.UnescapeDataString(pair.Substring(eq + 1)) : string.Empty;
            }
            return null;
        }

        public static void ClearQuery()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            IpgClearUrlQuery();
#endif
        }
    }
}
