mergeInto(LibraryManager.library, {
  WebGLFullscreen_Enter: function () {
    if (window.dicePokerFullscreen) {
      window.dicePokerFullscreen.enter();
    }
  },
  WebGLFullscreen_Toggle: function () {
    if (window.dicePokerFullscreen) {
      window.dicePokerFullscreen.toggle();
    }
  },
});
