// Browser-side half of IdlePowerGen.Managers.WebPage.
mergeInto(LibraryManager.library, {
  // Drops ?query from the address bar without reloading, so a one-off link such as
  // ?era=2 doesn't fire again when the player refreshes the tab.
  IpgClearUrlQuery: function () {
    if (window.history && window.history.replaceState)
      window.history.replaceState(null, '', window.location.pathname + window.location.hash);
  }
});
