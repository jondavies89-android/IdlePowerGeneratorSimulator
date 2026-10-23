#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace IdlePowerGen.Save
{
    // The one platform adapter for save persistence (CLAUDE.md: keep #if blocks in one place).
    // On Web, persistentDataPath is an in-memory filesystem mirrored to IndexedDB; a write
    // only survives a closed tab once it has been synced. Elsewhere the file is already on disk.
    public static class PersistentStorage
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void IdlePowerGen_SyncFileSystem();

        public static void Flush() => IdlePowerGen_SyncFileSystem();
#else
        public static void Flush() { }
#endif
    }
}
