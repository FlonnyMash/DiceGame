using System;
using System.Collections;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Networking;

namespace DiceGame.Infrastructure.Networking
{
    // In-memory INetworkService used for single-editor smoke tests of the lockstep pipeline.
    // SendAction is reflected back via OnActionReceived after a configurable latency, so the
    // serialize -> wire -> deserialize -> dispatch path runs end-to-end without a real backend.
    public class LocalLoopbackTransport : MonoBehaviour, INetworkService
    {
        [Header("Connection")]
        [SerializeField] private int _localPlayerId = 0;
        [SerializeField] private bool _isHost = true;

        [Header("Simulation")]
        [Tooltip("One-way delay in milliseconds before echoing a sent action back to OnActionReceived.")]
        [SerializeField, Min(0f)] private float _simulatedLatencyMs = 50f;

        public NetworkStatus Status { get; private set; } = NetworkStatus.Disconnected;
        public int LocalPlayerId => _localPlayerId;
        public bool IsHost => _isHost;

        public event Action<byte[]> OnActionReceived;
        public event Action<NetworkStatus> OnStatusChanged;

        public void Configure(int localPlayerId, bool isHost, float simulatedLatencyMs = 50f)
        {
            _localPlayerId = localPlayerId;
            _isHost = isHost;
            _simulatedLatencyMs = Mathf.Max(0f, simulatedLatencyMs);
        }

        private void OnEnable()
        {
            SetStatus(NetworkStatus.Connecting);
            // Loopback is "ready" immediately; defer to next frame so subscribers wired in OnEnable
            // of sibling components still see the state change.
            StartCoroutine(MarkConnectedNextFrame());
        }

        private void OnDisable()
        {
            SetStatus(NetworkStatus.Disconnected);
        }

        public void SendAction(byte[] data)
        {
            if (data == null) return;
            if (Status != NetworkStatus.Connected) return;

            byte[] copy = new byte[data.Length];
            Buffer.BlockCopy(data, 0, copy, 0, data.Length);
            StartCoroutine(EchoRoutine(copy));
        }

        private IEnumerator EchoRoutine(byte[] data)
        {
            if (_simulatedLatencyMs > 0f)
            {
                yield return new WaitForSeconds(_simulatedLatencyMs / 1000f);
            }
            else
            {
                yield return null;
            }

            OnActionReceived?.Invoke(data);
        }

        private IEnumerator MarkConnectedNextFrame()
        {
            yield return null;
            SetStatus(NetworkStatus.Connected);
        }

        private void SetStatus(NetworkStatus next)
        {
            if (Status == next) return;
            Status = next;
            OnStatusChanged?.Invoke(next);
        }
    }
}
