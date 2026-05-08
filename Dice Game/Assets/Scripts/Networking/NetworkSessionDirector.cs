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
    //   2. Outbound. Subscribe to the local player's IPlayerInput events and forward each
    //      one as a PlayerAction over the wire (lockstep: only commands cross).
    //   3. Inbound. Deserialize incoming bytes and route the PlayerAction to the
    //      NetworkPlayerInput that represents the originating remote player.
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

        // --- Inbound: wire -> NetworkPlayerInput ------------------------------------------

        private void HandleNetworkBytes(byte[] data)
        {
            if (!PlayerAction.TryDeserialize(data, out var action))
            {
                Debug.LogWarning("[NetworkSessionDirector] Dropping malformed PlayerAction packet.");
                return;
            }

            if (action.Type == PlayerActionType.Seed)
            {
                ApplySeed(action.Payload);
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
