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

        // Phase 2B: production transport selector. When true and IsOnline is true, GameController
        // creates a UgsNetworkTransport (Sessions + Relay + NGO byte pipe). When false, the legacy
        // LocalLoopbackTransport editor smoke-test path is used.
        public static bool UseRelay = false;

        // Host: populated after CreateSessionAsync succeeds (display in UI / share with peers).
        // Client: filled in by the menu before scene load so UgsNetworkTransport can JoinSessionByCodeAsync.
        public static string RelayJoinCode = null;

        public static void ResetToOffline()
        {
            IsOnline = false;
            IsHost = true;
            LocalPlayerId = 0;
            IsRemoteFlags = new List<bool>();
            UseRelay = false;
            RelayJoinCode = null;
        }
    }
}
