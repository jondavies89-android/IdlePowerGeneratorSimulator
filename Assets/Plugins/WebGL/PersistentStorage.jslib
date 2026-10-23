mergeInto(LibraryManager.library, {
  // Pushes the in-memory persistentDataPath to IndexedDB (false = memory -> storage).
  IdlePowerGen_SyncFileSystem: function () {
    FS.syncfs(false, function (err) {
      if (err) console.warn("[IdlePowerGen] Save sync failed: " + err);
    });
  }
});
