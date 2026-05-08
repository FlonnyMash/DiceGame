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

        event Action<byte[]> OnActionReceived;
        event Action<NetworkStatus> OnStatusChanged;
    }
}
