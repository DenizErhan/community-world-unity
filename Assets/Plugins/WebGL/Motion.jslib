// Lets the game honour the browser's "reduce motion" setting.
mergeInto(LibraryManager.library, {
  CW_PrefersReducedMotion: function () {
    return (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) ? 1 : 0;
  }
});
