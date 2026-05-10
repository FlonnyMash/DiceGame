using System;
using DiceGame.Core.Networking;

namespace DiceGame.Core.Interfaces
{
    public interface INetworkService
    {
        NetworkStatus Status { get; }
        int LocalPlayerId { get; }
        bool IsHost { get; }

        void SendAction(byte[] data);

        /// <summary>Wire payload plus Netcode client id of the sender (for lobby routing).</summary>
        event Action<byte[], ulong> OnActionReceived;
        event Action<NetworkStatus> OnStatusChanged;
    }
}
