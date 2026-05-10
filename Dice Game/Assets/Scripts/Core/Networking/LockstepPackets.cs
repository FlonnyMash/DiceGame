using System;
using System.Collections.Generic;
using System.Text;

namespace DiceGame.Core.Networking
{
    // Variable-length and extended packet formats that don't fit the fixed 9-byte PlayerAction
    // wire shape. NetworkSessionDirector dispatches incoming bytes via WireFormat.TryPeekType()
    // and then calls the right TryDeserialize on the corresponding struct here.
    //
    // All packets are framed as a single byte array. The CustomMessagingManager / NGO transport
    // is just a dumb byte-pipe, so the packet boundary is the message boundary -- no length
    // prefixes are needed across packets, only WITHIN them (e.g. NameLen, PlayerCount).

    // 13 bytes fixed: [Type=StateHash:1][PlayerId:4 LE][TurnIndex:4 LE][Hash:4 LE].
    public readonly struct StateHashPacket
    {
        public const int WireSize = 13;

        public int PlayerId { get; }
        public int TurnIndex { get; }
        public int Hash { get; }

        public StateHashPacket(int playerId, int turnIndex, int hash)
        {
            PlayerId = playerId;
            TurnIndex = turnIndex;
            Hash = hash;
        }

        public byte[] Serialize()
        {
            byte[] buffer = new byte[WireSize];
            buffer[0] = (byte)PlayerActionType.StateHash;
            WireFormat.WriteInt32LE(buffer, 1, PlayerId);
            WireFormat.WriteInt32LE(buffer, 5, TurnIndex);
            WireFormat.WriteInt32LE(buffer, 9, Hash);
            return buffer;
        }

        public static bool TryDeserialize(byte[] data, out StateHashPacket packet)
        {
            packet = default;
            if (data == null || data.Length < WireSize) return false;
            if (data[0] != (byte)PlayerActionType.StateHash) return false;

            int playerId = WireFormat.ReadInt32LE(data, 1);
            int turnIndex = WireFormat.ReadInt32LE(data, 5);
            int hash = WireFormat.ReadInt32LE(data, 9);
            packet = new StateHashPacket(playerId, turnIndex, hash);
            return true;
        }
    }

    // Variable: [Type=Identify:1][ClientToken:4 LE][NameLen:1][NameUtf8:N]. ClientToken is a random
    // non-zero int the peer chose locally; the host echoes it back in StartMatch so every guest can
    // find its slot without us coupling to NGO ClientId. NameLen is bounded to 64 to keep packets small.
    public readonly struct IdentifyPacket
    {
        public const int HeaderSize = 6; // Type(1) + ClientToken(4) + NameLen(1)
        public const int MaxNameBytes = 64;

        public int ClientToken { get; }
        public string Name { get; }

        public IdentifyPacket(int clientToken, string name)
        {
            ClientToken = clientToken;
            Name = name ?? string.Empty;
        }

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(Name ?? string.Empty);
            if (nameBytes.Length > MaxNameBytes)
            {
                Array.Resize(ref nameBytes, MaxNameBytes);
            }
            byte[] buffer = new byte[HeaderSize + nameBytes.Length];
            buffer[0] = (byte)PlayerActionType.Identify;
            WireFormat.WriteInt32LE(buffer, 1, ClientToken);
            buffer[5] = (byte)nameBytes.Length;
            Buffer.BlockCopy(nameBytes, 0, buffer, HeaderSize, nameBytes.Length);
            return buffer;
        }

        public static bool TryDeserialize(byte[] data, out IdentifyPacket packet)
        {
            packet = default;
            if (data == null || data.Length < HeaderSize) return false;
            if (data[0] != (byte)PlayerActionType.Identify) return false;

            int token = WireFormat.ReadInt32LE(data, 1);
            int nameLen = data[5];
            if (nameLen > MaxNameBytes) return false;
            if (data.Length < HeaderSize + nameLen) return false;

            string name = nameLen > 0 ? Encoding.UTF8.GetString(data, HeaderSize, nameLen) : string.Empty;
            packet = new IdentifyPacket(token, name);
            return true;
        }
    }

    public readonly struct RosterEntry
    {
        public int ClientToken { get; }
        public byte AssignedPlayerId { get; }
        public string Name { get; }

        public RosterEntry(int clientToken, byte assignedPlayerId, string name)
        {
            ClientToken = clientToken;
            AssignedPlayerId = assignedPlayerId;
            Name = name ?? string.Empty;
        }
    }

    // Variable roster broadcast. Wire layout:
    //   [Type=StartMatch:1][_pad:4=0][PlayerCount:1]
    //   then PlayerCount times: [ClientToken:4][AssignedPlayerId:1][NameLen:1][NameUtf8:N]
    //
    // The leading 4-byte pad keeps offset 1..4 reserved (mirroring the fixed format's PlayerId slot)
    // so anything that only needs to peek at the type/header doesn't have to special-case this.
    public readonly struct StartMatchPacket
    {
        public const int HeaderSize = 6; // Type(1) + Pad(4) + PlayerCount(1)
        public const int EntryHeaderSize = 6; // ClientToken(4) + AssignedPlayerId(1) + NameLen(1)
        public const int MaxPlayers = 4;

        public IReadOnlyList<RosterEntry> Roster { get; }

        public StartMatchPacket(IReadOnlyList<RosterEntry> roster)
        {
            Roster = roster ?? Array.Empty<RosterEntry>();
        }

        public byte[] Serialize()
        {
            int totalSize = HeaderSize;
            byte[][] nameBlobs = new byte[Roster.Count][];
            for (int i = 0; i < Roster.Count; i++)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(Roster[i].Name ?? string.Empty);
                if (nameBytes.Length > IdentifyPacket.MaxNameBytes)
                {
                    Array.Resize(ref nameBytes, IdentifyPacket.MaxNameBytes);
                }
                nameBlobs[i] = nameBytes;
                totalSize += EntryHeaderSize + nameBytes.Length;
            }

            byte[] buffer = new byte[totalSize];
            buffer[0] = (byte)PlayerActionType.StartMatch;
            WireFormat.WriteInt32LE(buffer, 1, 0); // pad
            buffer[5] = (byte)Roster.Count;

            int cursor = HeaderSize;
            for (int i = 0; i < Roster.Count; i++)
            {
                WireFormat.WriteInt32LE(buffer, cursor, Roster[i].ClientToken);
                buffer[cursor + 4] = Roster[i].AssignedPlayerId;
                buffer[cursor + 5] = (byte)nameBlobs[i].Length;
                Buffer.BlockCopy(nameBlobs[i], 0, buffer, cursor + EntryHeaderSize, nameBlobs[i].Length);
                cursor += EntryHeaderSize + nameBlobs[i].Length;
            }

            return buffer;
        }

        public static bool TryDeserialize(byte[] data, out StartMatchPacket packet)
        {
            packet = default;
            if (data == null || data.Length < HeaderSize) return false;
            if (data[0] != (byte)PlayerActionType.StartMatch) return false;

            int playerCount = data[5];
            if (playerCount < 0 || playerCount > MaxPlayers) return false;

            var roster = new List<RosterEntry>(playerCount);
            int cursor = HeaderSize;
            for (int i = 0; i < playerCount; i++)
            {
                if (data.Length < cursor + EntryHeaderSize) return false;
                int token = WireFormat.ReadInt32LE(data, cursor);
                byte assignedId = data[cursor + 4];
                int nameLen = data[cursor + 5];
                if (nameLen > IdentifyPacket.MaxNameBytes) return false;
                int entryEnd = cursor + EntryHeaderSize + nameLen;
                if (data.Length < entryEnd) return false;

                string name = nameLen > 0 ? Encoding.UTF8.GetString(data, cursor + EntryHeaderSize, nameLen) : string.Empty;
                roster.Add(new RosterEntry(token, assignedId, name));
                cursor = entryEnd;
            }

            packet = new StartMatchPacket(roster);
            return true;
        }
    }
}
