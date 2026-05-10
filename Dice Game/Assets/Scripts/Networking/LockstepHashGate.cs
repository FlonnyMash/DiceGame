using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Models;
using DiceGame.Core.Networking;
using DiceGame.Core.Systems;

namespace DiceGame.Networking
{
    // Host-arbitrated turn barrier + desync detector.
    //
    // Wire flow per turn (4-player example):
    //   1. Every peer's local MatchManager fires OnTurnEnded after applying the final action of
    //      the turn (Category / BonusClaim).
    //   2. This gate immediately computes LockstepHasher(match, turnIndex) and broadcasts a
    //      StateHash packet. The host also records its own hash locally because UgsNetworkTransport
    //      doesn't loop SendAction back to the sender.
    //   3. The host collects hashes for that turnIndex into _hostInbox. Once it has one entry per
    //      configured player, it compares them all against its own:
    //        - all match  -> broadcast SyncOk(turnIndex), clear locally, fire OnSyncOk.
    //        - mismatch   -> fire OnDesyncDetected; HasFailed = true, the GameController aborts.
    //   4. Guests just wait for the SyncOk packet. When it arrives they clear locally and fire
    //      OnSyncOk; until then the GameController's TurnTransitionRoutine yields on
    //      WaitForClearance so AdvanceToNextTurn doesn't run.
    //
    // Timeouts (per-turn watchdog coroutine):
    //   - Soft  (default 2s): fires OnSyncStalling so the overlay can switch from a normal
    //     "connecting" message to err_connection_unstable. The match is NOT aborted yet.
    //   - Hard (default 10s): fires OnSyncFailed which the GameController turns into AbortMatch.
    //
    // Lockstep purity: this is the ONLY component that talks about hashes / sync. It writes/reads
    // bytes via the existing INetworkService byte-pipe, never via NetworkVariable / RPC.
    public class LockstepHashGate : MonoBehaviour
    {
        public event Action<int> OnSyncOk;
        public event Action<int> OnDesyncDetected;
        public event Action<int> OnSyncStalling;
        public event Action<int> OnSyncFailed;

        [Header("Timeouts (seconds)")]
        [SerializeField] private float _softTimeoutSeconds = 2f;
        [SerializeField] private float _hardTimeoutSeconds = 10f;

        private INetworkService _transport;
        private MatchManager _match;
        private int _playerCount;
        private bool _isHost;
        private bool _running;

        public bool HasFailed { get; private set; }
        public int LastFailedTurnIndex { get; private set; } = -1;

        private readonly HashSet<int> _clearedTurns = new HashSet<int>();
        private readonly Dictionary<int, int> _localHashByTurn = new Dictionary<int, int>();
        private readonly Dictionary<int, Dictionary<int, int>> _hostInbox = new Dictionary<int, Dictionary<int, int>>();
        private readonly Dictionary<int, Coroutine> _watchdogs = new Dictionary<int, Coroutine>();
        private readonly HashSet<int> _stallingFiredTurns = new HashSet<int>();

        public void Configure(INetworkService transport, MatchManager match, int playerCount, bool isHost)
        {
            _transport = transport;
            _match = match;
            _playerCount = Mathf.Max(1, playerCount);
            _isHost = isHost;
        }

        // Hook the MatchManager's OnTurnEnded once everything is wired. Idempotent.
        public void Begin()
        {
            if (_running) return;
            if (_match == null)
            {
                Debug.LogError("[LockstepHashGate] Begin() before Configure(). Aborting.");
                return;
            }
            _running = true;
            _match.OnTurnEnded += HandleTurnEnded;
        }

        public void Stop()
        {
            if (!_running) return;
            _running = false;
            if (_match != null) _match.OnTurnEnded -= HandleTurnEnded;
            StopAllWatchdogs();
        }

        private void OnDestroy() => Stop();

        public bool IsCleared(int turnIndex) => _clearedTurns.Contains(turnIndex);

        // Coroutine helper: GameController yields on this between the deterministic transition
        // animation and MatchManager.AdvanceToNextTurn so the next turn never starts before all
        // peers have reported a matching state hash.
        public IEnumerator WaitForClearance(int turnIndex)
        {
            while (!IsCleared(turnIndex) && !HasFailed)
            {
                yield return null;
            }
        }

        // --- packet entry points (called by NetworkSessionDirector) -----------------------

        public void HandleStateHash(StateHashPacket packet)
        {
            if (!_isHost) return; // guests don't arbitrate; they wait for SyncOk
            RecordHostHash(packet.PlayerId, packet.TurnIndex, packet.Hash);
            CheckHostMatch(packet.TurnIndex);
        }

        public void HandleSyncOk(int turnIndex)
        {
            if (_isHost) return; // host already cleared the moment it broadcast SyncOk
            ClearTurn(turnIndex);
        }

        // --- internals --------------------------------------------------------------------

        private void HandleTurnEnded(Player _)
        {
            if (HasFailed) return;
            if (_match == null || _transport == null) return;

            int t = _match.TurnIndex;
            int hash = LockstepHasher.Compute(_match, t);
            _localHashByTurn[t] = hash;

            // Broadcast our hash to every other peer.
            int localId = _transport.LocalPlayerId;
            var packet = new StateHashPacket(localId, t, hash);
            _transport.SendAction(packet.Serialize());

            if (_isHost)
            {
                // Self-record: the host's own SendAction does NOT loop back.
                RecordHostHash(localId, t, hash);
                CheckHostMatch(t);
            }

            // Even after a full host-side match, the watchdog covers the host's own SyncOk
            // delivery edge and helps detect "host wedged" cases on the guest side.
            StartWatchdog(t);
        }

        private void RecordHostHash(int playerId, int turnIndex, int hash)
        {
            if (!_hostInbox.TryGetValue(turnIndex, out var dict))
            {
                dict = new Dictionary<int, int>();
                _hostInbox[turnIndex] = dict;
            }
            dict[playerId] = hash;
        }

        private void CheckHostMatch(int turnIndex)
        {
            if (_clearedTurns.Contains(turnIndex)) return;
            if (!_hostInbox.TryGetValue(turnIndex, out var dict)) return;
            if (dict.Count < _playerCount) return;

            int expected = _localHashByTurn.TryGetValue(turnIndex, out var local) ? local : 0;
            foreach (var kvp in dict)
            {
                if (kvp.Value != expected)
                {
                    HasFailed = true;
                    LastFailedTurnIndex = turnIndex;
                    StopWatchdog(turnIndex);
                    OnDesyncDetected?.Invoke(turnIndex);
                    return;
                }
            }

            // All hashes match: tell every peer to proceed.
            int hostId = _transport != null ? _transport.LocalPlayerId : 0;
            var ok = new PlayerAction(PlayerActionType.SyncOk, hostId, turnIndex);
            _transport?.SendAction(ok.Serialize());
            ClearTurn(turnIndex);
        }

        private void ClearTurn(int turnIndex)
        {
            if (!_clearedTurns.Add(turnIndex)) return;
            StopWatchdog(turnIndex);
            OnSyncOk?.Invoke(turnIndex);
        }

        private void StartWatchdog(int turnIndex)
        {
            StopWatchdog(turnIndex);
            _stallingFiredTurns.Remove(turnIndex);
            _watchdogs[turnIndex] = StartCoroutine(WatchdogRoutine(turnIndex));
        }

        private void StopWatchdog(int turnIndex)
        {
            if (_watchdogs.TryGetValue(turnIndex, out var c) && c != null)
            {
                StopCoroutine(c);
            }
            _watchdogs.Remove(turnIndex);
        }

        private void StopAllWatchdogs()
        {
            foreach (var c in _watchdogs.Values)
            {
                if (c != null) StopCoroutine(c);
            }
            _watchdogs.Clear();
            _stallingFiredTurns.Clear();
        }

        private IEnumerator WatchdogRoutine(int turnIndex)
        {
            float elapsed = 0f;
            const float step = 0.1f;
            while (!_clearedTurns.Contains(turnIndex) && !HasFailed)
            {
                yield return new WaitForSeconds(step);
                elapsed += step;

                if (elapsed >= _softTimeoutSeconds && _stallingFiredTurns.Add(turnIndex))
                {
                    OnSyncStalling?.Invoke(turnIndex);
                }

                if (elapsed >= _hardTimeoutSeconds)
                {
                    HasFailed = true;
                    LastFailedTurnIndex = turnIndex;
                    OnSyncFailed?.Invoke(turnIndex);
                    _watchdogs.Remove(turnIndex);
                    yield break;
                }
            }
        }
    }
}
