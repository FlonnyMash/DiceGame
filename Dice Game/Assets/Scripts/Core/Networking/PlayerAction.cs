using System;

namespace DiceGame.Core.Networking
{
    public enum PlayerActionType : byte
    {
        // Fixed 9-byte format: [Type:1][PlayerId:4 LE][Payload:4 LE].
        Seed = 0,
        Roll = 1,
        ToggleHold = 2,
        Category = 3,
        BonusClaim = 4,

        // Extended 13-byte format: [Type:1][PlayerId:4][TurnIndex:4][Hash:4]. See LockstepPackets.cs.
        StateHash = 5,

        // Fixed 9-byte: [Type:1][PlayerId:4=hostId][Payload:4=TurnIndex]. Host-arbitrated turn barrier.
        SyncOk = 6,

        // Variable: [Type:1][ClientToken:4][NameLen:1][NameUtf8:N]. Pre-match peer name + handle.
        Identify = 7,

        // Variable roster broadcast. See StartMatchPacket in LockstepPackets.cs.
        StartMatch = 8,

        // Fixed 9-byte: [Type:1][PlayerId:4][Payload:4=AbortReason]. Best-effort match-abort signal.
        Abort = 9
    }

    public enum AbortReason
    {
        Unknown = 0,
        Desync = 1,
        PeerDrop = 2,
        HostLeft = 3,
        UserQuit = 4
    }

    // PlayerAction covers the fixed-9-byte packets only: Seed, Roll, ToggleHold, Category,
    // BonusClaim, SyncOk, Abort. Variable / extended packets (StateHash, Identify, StartMatch)
    // live in LockstepPackets.cs and are dispatched alongside PlayerAction by NetworkSessionDirector.
    //
    // Wire format (fixed): [ActionType:1][PlayerId:4 LE][Payload:4 LE].
    // Payload meaning:
    //   Seed       -> dice RNG seed (non-zero on the host).
    //   Roll       -> unused (0).
    //   ToggleHold -> die index in [0, DiceCup.Dice.Count).
    //   Category   -> (int)ScoreCategory.
    //   BonusClaim -> unused (0).
    //   SyncOk     -> turn index whose state hash matched on every peer.
    //   Abort      -> (int)AbortReason.
    public readonly struct PlayerAction
    {
        public const int WireSize = 9;

        public PlayerActionType Type { get; }
        public int PlayerId { get; }
        public int Payload { get; }

        public PlayerAction(PlayerActionType type, int playerId, int payload = 0)
        {
            Type = type;
            PlayerId = playerId;
            Payload = payload;
        }

        public byte[] Serialize()
        {
            byte[] buffer = new byte[WireSize];
            buffer[0] = (byte)Type;
            WireFormat.WriteInt32LE(buffer, 1, PlayerId);
            WireFormat.WriteInt32LE(buffer, 5, Payload);
            return buffer;
        }

        // Returns true iff the buffer carries one of the fixed 9-byte packet types covered by this
        // struct. Callers that receive other packet types (StateHash / Identify / StartMatch) should
        // dispatch via LockstepPackets.* using PeekType() first.
        public static bool TryDeserialize(byte[] data, out PlayerAction action)
        {
            action = default;
            if (data == null || data.Length < WireSize) return false;

            byte rawType = data[0];
            if (!IsFixedFormatType(rawType)) return false;

            int playerId = WireFormat.ReadInt32LE(data, 1);
            int payload = WireFormat.ReadInt32LE(data, 5);

            action = new PlayerAction((PlayerActionType)rawType, playerId, payload);
            return true;
        }

        private static bool IsFixedFormatType(byte raw)
        {
            // All fixed-format types listed explicitly so adding a new variable-length type
            // doesn't accidentally widen this acceptance set.
            switch ((PlayerActionType)raw)
            {
                case PlayerActionType.Seed:
                case PlayerActionType.Roll:
                case PlayerActionType.ToggleHold:
                case PlayerActionType.Category:
                case PlayerActionType.BonusClaim:
                case PlayerActionType.SyncOk:
                case PlayerActionType.Abort:
                    return true;
                default:
                    return false;
            }
        }

        public override string ToString()
            => $"PlayerAction(Type={Type}, PlayerId={PlayerId}, Payload={Payload})";
    }

    // Shared little-endian helpers used across all lockstep packets. Centralised here so the
    // serialization layer never depends on BitConverter.IsLittleEndian (which is platform-specific).
    internal static class WireFormat
    {
        public static void WriteInt32LE(byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static int ReadInt32LE(byte[] buffer, int offset)
        {
            return buffer[offset + 0]
                 | (buffer[offset + 1] << 8)
                 | (buffer[offset + 2] << 16)
                 | (buffer[offset + 3] << 24);
        }

        public static bool TryPeekType(byte[] data, out PlayerActionType type)
        {
            type = default;
            if (data == null || data.Length < 1) return false;
            type = (PlayerActionType)data[0];
            return true;
        }
    }
}
