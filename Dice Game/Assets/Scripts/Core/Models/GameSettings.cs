using System.Collections.Generic;

namespace DiceGame.Core.Models
{
    public static class GameSettings
    {
        // Wenn das Spiel gestartet wird, gibt es standardmäßig immer einen Spieler 1.
        // Das ist auch nützlich, falls du die InGameScene direkt im Editor testest.
        public static List<string> PlayerNames = new List<string> { "Spieler 1" };
    }
}