using System.Collections.Generic;

namespace DiceGame.Core.Models
{
    public static class MatchData
    {
        // Defaults preserve existing single-player / local-multiplayer behaviour.
        public static List<string> PlayerNames = new List<string> { "You" };

        // Online-mode metadata. When IsOnline is false, GameController falls back to the
        // legacy local/bot setup path and ignores everything below.
        public static bool IsOnline = false;
        public static bool IsHost = true;
        public static int LocalPlayerId = 0;

        // One flag per entry in PlayerNames: true if that seat is owned by a remote peer.
        public static List<bool> IsRemoteFlags = new List<bool>();

        // Phase 2C: production online flow always uses Unity Sessions + Relay. The lobby populates
        // this code so GameController can re-display it (e.g. on a "share with peers" overlay if
        // we ever bring back an in-match share button).
        public static string RelayJoinCode = null;

        public static void ResetToOffline()
        {
            IsOnline = false;
            IsHost = true;
            LocalPlayerId = 0;
            IsRemoteFlags = new List<bool>();
            RelayJoinCode = null;
        }
    }
}
