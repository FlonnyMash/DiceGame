using System;

namespace DiceGame.Core.Networking
{
    public enum PlayerActionType : byte
    {
        Seed = 0,
        Roll = 1,
        ToggleHold = 2,
        Category = 3,
        BonusClaim = 4
    }

    // Wire format (9 bytes, fixed): [ActionType:1][PlayerId:4 LE][Payload:4 LE].
    // Payload meaning:
    //   Seed       -> dice RNG seed (non-zero on the host).
    //   Roll       -> unused (0).
    //   ToggleHold -> die index in [0, DiceCup.Dice.Count).
    //   Category   -> (int)ScoreCategory.
    //   BonusClaim -> unused (0).
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
            WriteInt32LE(buffer, 1, PlayerId);
            WriteInt32LE(buffer, 5, Payload);
            return buffer;
        }

        public static bool TryDeserialize(byte[] data, out PlayerAction action)
        {
            action = default;
            if (data == null || data.Length < WireSize) return false;

            byte rawType = data[0];
            if (rawType > (byte)PlayerActionType.BonusClaim) return false;

            int playerId = ReadInt32LE(data, 1);
            int payload = ReadInt32LE(data, 5);

            action = new PlayerAction((PlayerActionType)rawType, playerId, payload);
            return true;
        }

        private static void WriteInt32LE(byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static int ReadInt32LE(byte[] buffer, int offset)
        {
            return buffer[offset + 0]
                 | (buffer[offset + 1] << 8)
                 | (buffer[offset + 2] << 16)
                 | (buffer[offset + 3] << 24);
        }

        public override string ToString()
            => $"PlayerAction(Type={Type}, PlayerId={PlayerId}, Payload={Payload})";
    }
}
