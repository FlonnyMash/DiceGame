using System;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Inputs;
using DiceGame.Core.Networking;
using DiceGame.Core.Rules;

namespace DiceGame.Networking
{
    // Glue between INetworkService and the per-player IPlayerInput instances.
    //
    // Responsibilities:
    //   1. Seed handshake. Host generates a non-zero dice RNG seed and broadcasts it.
    //      Clients wait for the Seed action before constructing MatchManager.
    //   2. Outbound (gameplay actions). Subscribe to the local player's IPlayerInput events and
    //      forward each one as a PlayerAction over the wire. Lockstep: only commands cross.
    //   3. Inbound (gameplay actions). Deserialize incoming bytes and route the PlayerAction to
    //      the NetworkPlayerInput that represents the originating remote player.
    //   4. Phase 2C dispatch. New packet types (StateHash / SyncOk / Abort) are surfaced as
    //      typed events for LockstepHashGate / GameController to consume. Lobby-only packets
    //      (Identify / StartMatch) are ignored here -- they're owned by OnlineLobbyController
    //      while it has the transport, and never appear during an in-match session.
    public class NetworkSessionDirector : MonoBehaviour
    {
        private INetworkService _networkService;
        private IPlayerInput _localInput;
        private readonly Dictionary<int, NetworkPlayerInput> _remoteInputs = new Dictionary<int, NetworkPlayerInput>();

        public int Seed { get; private set; }
        public bool HasSeed { get; private set; }
        public NetworkStatus Status => _networkService != null ? _networkService.Status : NetworkStatus.Disconnected;

        public event Action<int> OnSeedReceived;
        public event Action<NetworkStatus> OnStatusChanged;

        // Phase 2C events. Subscribed by LockstepHashGate (StateHash / SyncOk) and GameController
        // (Abort), defined here so the director remains the single packet-dispatch fan-out.
        public event Action<StateHashPacket> OnStateHashReceived;
        public event Action<int> OnSyncOkReceived;          // payload: turnIndex
        public event Action<AbortReason> OnAbortReceived;

        public void Configure(INetworkService networkService, IPlayerInput localInput, IEnumerable<NetworkPlayerInput> remoteInputs)
        {
            DetachAll();

            _networkService = networkService;
            _localInput = localInput;

            _remoteInputs.Clear();
            if (remoteInputs != null)
            {
                foreach (var remote in remoteInputs)
                {
                    if (remote == null) continue;
                    _remoteInputs[remote.PlayerId] = remote;
                }
            }

            if (_networkService != null)
            {
                _networkService.OnActionReceived += HandleNetworkBytes;
                _networkService.OnStatusChanged += HandleStatusChanged;
            }

            if (_localInput != null)
            {
                _localInput.OnRollRequested += HandleLocalRoll;
                _localInput.OnToggleHoldRequested += HandleLocalToggleHold;
                _localInput.OnCategoryRequested += HandleLocalCategory;
                _localInput.OnBonusClaimRequested += HandleLocalBonusClaim;
            }
        }

        public void BeginHandshake()
        {
            if (_networkService == null) return;

            if (_networkService.Status == NetworkStatus.Connected)
            {
                ExecuteHandshake();
            }
            else
            {
                _networkService.OnStatusChanged += WaitForConnectedThenHandshake;
            }
        }

        private void WaitForConnectedThenHandshake(NetworkStatus status)
        {
            if (status != NetworkStatus.Connected) return;
            _networkService.OnStatusChanged -= WaitForConnectedThenHandshake;
            ExecuteHandshake();
        }

        private void ExecuteHandshake()
        {
            // Client: do nothing here. The Seed action will arrive via OnActionReceived.
            if (!_networkService.IsHost) return;

            int seed = GenerateSeed();
            BroadcastSeed(seed);
            ApplySeed(seed);
        }

        private static int GenerateSeed()
        {
            return UnityEngine.Random.Range(1, int.MaxValue);
        }

        private void BroadcastSeed(int seed)
        {
            if (_networkService == null) return;
            var action = new PlayerAction(PlayerActionType.Seed, _networkService.LocalPlayerId, seed);
            _networkService.SendAction(action.Serialize());
        }

        private void ApplySeed(int seed)
        {
            if (HasSeed) return;
            Seed = seed;
            HasSeed = true;
            OnSeedReceived?.Invoke(seed);
        }

        // --- Outbound: local input -> wire -------------------------------------------------

        private void HandleLocalRoll()
            => Broadcast(new PlayerAction(PlayerActionType.Roll, LocalId()));

        private void HandleLocalToggleHold(int dieIndex)
            => Broadcast(new PlayerAction(PlayerActionType.ToggleHold, LocalId(), dieIndex));

        private void HandleLocalCategory(ScoreCategory category)
            => Broadcast(new PlayerAction(PlayerActionType.Category, LocalId(), (int)category));

        private void HandleLocalBonusClaim()
            => Broadcast(new PlayerAction(PlayerActionType.BonusClaim, LocalId()));

        private int LocalId() => _networkService != null ? _networkService.LocalPlayerId : -1;

        private void Broadcast(PlayerAction action)
        {
            if (_networkService == null) return;
            if (_networkService.Status != NetworkStatus.Connected) return;
            _networkService.SendAction(action.Serialize());
        }

        public void BroadcastAbort(AbortReason reason)
        {
            if (_networkService == null) return;
            if (_networkService.Status != NetworkStatus.Connected) return;
            var action = new PlayerAction(PlayerActionType.Abort, _networkService.LocalPlayerId, (int)reason);
            _networkService.SendAction(action.Serialize());
        }

        // --- Inbound: wire -> NetworkPlayerInput / typed events ---------------------------

        private void HandleNetworkBytes(byte[] data, ulong _)
        {
            if (data == null || data.Length == 0) return;
            if (!WireFormat.TryPeekType(data, out var type))
            {
                Debug.LogWarning("[NetworkSessionDirector] Empty packet; dropping.");
                return;
            }

            switch (type)
            {
                case PlayerActionType.StateHash:
                    if (StateHashPacket.TryDeserialize(data, out var hashPacket))
                    {
                        OnStateHashReceived?.Invoke(hashPacket);
                    }
                    return;

                case PlayerActionType.Identify:
                case PlayerActionType.StartMatch:
                    // Lobby-only. Should not appear here in a healthy session; ignore.
                    return;

                case PlayerActionType.Seed:
                case PlayerActionType.Roll:
                case PlayerActionType.ToggleHold:
                case PlayerActionType.Category:
                case PlayerActionType.BonusClaim:
                case PlayerActionType.SyncOk:
                case PlayerActionType.Abort:
                    if (PlayerAction.TryDeserialize(data, out var action))
                    {
                        DispatchFixedAction(action);
                    }
                    else
                    {
                        Debug.LogWarning("[NetworkSessionDirector] Malformed fixed-format packet.");
                    }
                    return;

                default:
                    Debug.LogWarning($"[NetworkSessionDirector] Unknown packet type {(byte)type}; dropping.");
                    return;
            }
        }

        private void DispatchFixedAction(PlayerAction action)
        {
            switch (action.Type)
            {
                case PlayerActionType.Seed:
                    ApplySeed(action.Payload);
                    return;

                case PlayerActionType.SyncOk:
                    OnSyncOkReceived?.Invoke(action.Payload);
                    return;

                case PlayerActionType.Abort:
                    OnAbortReceived?.Invoke((AbortReason)action.Payload);
                    return;
            }

            // Skip echoes of our own commands. The local MatchManager already applied them
            // via the LocalPlayerInput pipeline; replaying would double-apply.
            if (_networkService != null && action.PlayerId == _networkService.LocalPlayerId) return;

            if (_remoteInputs.TryGetValue(action.PlayerId, out var remote) && remote != null)
            {
                remote.EnqueueRemoteAction(action);
            }
            else
            {
                Debug.LogWarning($"[NetworkSessionDirector] No NetworkPlayerInput registered for PlayerId={action.PlayerId}; dropping {action.Type}.");
            }
        }

        private void HandleStatusChanged(NetworkStatus status)
        {
            OnStatusChanged?.Invoke(status);
        }

        private void OnDestroy()
        {
            DetachAll();
        }

        private void DetachAll()
        {
            if (_networkService != null)
            {
                _networkService.OnActionReceived -= HandleNetworkBytes;
                _networkService.OnStatusChanged -= HandleStatusChanged;
                _networkService.OnStatusChanged -= WaitForConnectedThenHandshake;
            }

            if (_localInput != null)
            {
                _localInput.OnRollRequested -= HandleLocalRoll;
                _localInput.OnToggleHoldRequested -= HandleLocalToggleHold;
                _localInput.OnCategoryRequested -= HandleLocalCategory;
                _localInput.OnBonusClaimRequested -= HandleLocalBonusClaim;
            }
        }
    }
}
