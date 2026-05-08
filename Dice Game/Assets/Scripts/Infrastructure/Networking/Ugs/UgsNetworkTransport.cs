using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Networking;

namespace DiceGame.Infrastructure.Networking.Ugs
{
    // Production INetworkService backed by Unity Sessions + Relay (com.unity.services.multiplayer)
    // and NGO's CustomMessagingManager as a dumb byte-pipe. No NetworkVariable / NetworkTransform.
    //
    // Wire path (host -> client):
    //   SendAction(bytes) -> SendNamedMessage("DiceLockstep", clientIds, FastBufferWriter)
    //                     -> Relay -> client's HandleNamedMessage -> OnActionReceived(bytes)
    //
    // Wire path (client -> client via host relay):
    //   client.SendAction(bytes) -> SendNamedMessage(server, ...)
    //                            -> host.HandleNamedMessage(senderId, bytes)
    //                            -> ForwardToPeers(senderId, bytes) + OnActionReceived(bytes)
    //                            -> peers receive via their HandleNamedMessage -> OnActionReceived(bytes)
    public class UgsNetworkTransport : MonoBehaviour, INetworkService
    {
        public const string MessageName = "DiceLockstep";

        // Cross-instance shutdown coordination: a freshly-spawned transport awaits this Task before
        // creating a new session, so any LeaveAsync / NGO Shutdown from a previous match has fully
        // wound down. Persists between Play sessions in the editor and across scene loads at runtime.
        private static Task s_PreviousShutdownTask = Task.CompletedTask;

        public NetworkStatus Status { get; private set; } = NetworkStatus.Disconnected;
        public int LocalPlayerId { get; private set; }
        public bool IsHost { get; private set; }

        public string JoinCode { get; private set; }
        public event Action<string> OnJoinCodeReady;

        public event Action<byte[]> OnActionReceived;
        public event Action<NetworkStatus> OnStatusChanged;

        private int _expectedPlayerCount = 2;
        private string _joinCodeForClient;
        private int _connectedPeerCount;
        private bool _handlerRegistered;
        private ISession _session;
        private CancellationTokenSource _bootstrapCts;
        private bool _disposed;
        private readonly List<ulong> _broadcastBuffer = new List<ulong>();

        public void Configure(int localPlayerId, bool isHost, int expectedPlayerCount, string joinCodeForClient = null)
        {
            LocalPlayerId = localPlayerId;
            IsHost = isHost;
            _expectedPlayerCount = Mathf.Max(2, expectedPlayerCount);
            _joinCodeForClient = joinCodeForClient;
        }

        private void OnEnable()
        {
            _disposed = false;
            _bootstrapCts = new CancellationTokenSource();
            SetStatus(NetworkStatus.Connecting);
            // RunBootstrapAsync is started on Unity's main-thread SynchronizationContext (we never
            // call ConfigureAwait(false)), so every continuation -- including the NGO/SDK calls
            // wrapped by WithRelayNetwork() -- resumes on the main thread.
            _ = RunBootstrapAsync(_bootstrapCts.Token);
        }

        private async Task RunBootstrapAsync(CancellationToken ct)
        {
            try
            {
                // Wait for any previous transport's Leave/Shutdown to finish before claiming UGS state.
                try { await s_PreviousShutdownTask; } catch { /* prior failure is irrelevant here */ }
                if (ct.IsCancellationRequested || _disposed) return;

                // (1) Safe UGS initialization: idempotent. Spin if another caller is mid-init.
                while (UnityServices.State == ServicesInitializationState.Initializing && !ct.IsCancellationRequested)
                {
                    await Task.Yield();
                }
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    // Per-instance auth profile prevents two instances on the same machine (e.g.
                    // Editor + standalone build) from sharing the cached anonymous identity. If
                    // they share, both calls to CreateSessionAsync return the SAME lobby because
                    // the Lobby service treats them as the same player, which makes self-join
                    // attempts fail with a confusing "invalid character" cached error from the
                    // previous attempt.
                    var initOptions = new InitializationOptions().SetProfile(GetAuthProfile());
                    await UnityServices.InitializeAsync(initOptions);
                }
                if (ct.IsCancellationRequested || _disposed) return;

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                if (ct.IsCancellationRequested || _disposed) return;

                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    Debug.LogError("[UgsNetworkTransport] NetworkManager.Singleton is null. Add a NetworkManager prefab to the scene.");
                    SetStatus(NetworkStatus.Error);
                    return;
                }

                // Defensive: drain any leftover NGO state from a prior session before the SDK
                // tries to StartHost/StartClient on the persistent NetworkManager.
                while (nm.ShutdownInProgress && !ct.IsCancellationRequested) await Task.Yield();
                if (nm.IsListening || nm.IsHost || nm.IsServer || nm.IsConnectedClient)
                {
                    Debug.LogWarning("[UgsNetworkTransport] NetworkManager was still active on bootstrap; forcing shutdown.");
                    try { nm.Shutdown(true); } catch (Exception e) { Debug.LogWarning($"[UgsNetworkTransport] Pre-bootstrap Shutdown threw: {e.Message}"); }
                    while (nm.ShutdownInProgress && !ct.IsCancellationRequested) await Task.Yield();
                    // One additional yield so NGO finishes its tail tick before StartHost.
                    await Task.Yield();
                }
                if (ct.IsCancellationRequested || _disposed) return;

                nm.OnServerStarted += HandleServerStarted;
                nm.OnClientConnectedCallback += HandleClientConnected;
                nm.OnClientDisconnectCallback += HandleClientDisconnected;
                nm.OnTransportFailure += HandleTransportFailure;

                if (IsHost)
                {
                    var options = new SessionOptions
                    {
                        Name = "DiceGameMatch",
                        MaxPlayers = _expectedPlayerCount,
                        IsPrivate = false
                    }.WithRelayNetwork();

                    _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                    if (ct.IsCancellationRequested || _disposed) return;
                    JoinCode = _session.Code;
                    Debug.Log($"[UgsNetworkTransport] Host created session. JoinCode={JoinCode} bytes=[{DescribeCodeBytes(JoinCode)}] sessionId={_session.Id}");
                    OnJoinCodeReady?.Invoke(JoinCode);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(_joinCodeForClient))
                    {
                        Debug.LogError("[UgsNetworkTransport] Client started with empty join code.");
                        SetStatus(NetworkStatus.Error);
                        return;
                    }

                    Debug.Log($"[UgsNetworkTransport] Client joining. Code={_joinCodeForClient} bytes=[{DescribeCodeBytes(_joinCodeForClient)}]");
                    _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(_joinCodeForClient);
                    if (ct.IsCancellationRequested || _disposed) return;
                    Debug.Log($"[UgsNetworkTransport] Client joined session. Code={_joinCodeForClient}");
                }
            }
            catch (OperationCanceledException) { /* transport disposed mid-bootstrap; nothing to do */ }
            catch (Exception e)
            {
                Debug.LogError($"[UgsNetworkTransport] Bootstrap failed: {e}");
                if (!_disposed) SetStatus(NetworkStatus.Error);
            }
        }

        private void HandleServerStarted()
        {
            if (!IsHost) return;
            RegisterMessageHandler();
        }

        private void HandleClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (IsHost)
            {
                if (clientId == nm.LocalClientId) return;
                _connectedPeerCount++;
                if (_connectedPeerCount >= _expectedPlayerCount - 1)
                {
                    SetStatus(NetworkStatus.Connected);
                }
            }
            else
            {
                if (clientId != nm.LocalClientId) return;
                RegisterMessageHandler();
                SetStatus(NetworkStatus.Connected);
            }
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (IsHost)
            {
                if (clientId == nm.LocalClientId) { SetStatus(NetworkStatus.Disconnected); return; }
                _connectedPeerCount = Mathf.Max(0, _connectedPeerCount - 1);
                SetStatus(NetworkStatus.Disconnected);
            }
            else
            {
                if (clientId != nm.LocalClientId) return;
                SetStatus(NetworkStatus.Disconnected);
            }
        }

        private void HandleTransportFailure() => SetStatus(NetworkStatus.Error);

        private void RegisterMessageHandler()
        {
            if (_handlerRegistered) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, HandleNamedMessage);
            _handlerRegistered = true;
        }

        private void UnregisterMessageHandler()
        {
            if (!_handlerRegistered) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.CustomMessagingManager != null)
            {
                nm.CustomMessagingManager.UnregisterNamedMessageHandler(MessageName);
            }
            _handlerRegistered = false;
        }

        private void HandleNamedMessage(ulong senderId, FastBufferReader reader)
        {
            int length = reader.Length - reader.Position;
            if (length <= 0) return;

            byte[] bytes = new byte[length];
            reader.ReadBytesSafe(ref bytes, length);

            if (IsHost && senderId != NetworkManager.Singleton.LocalClientId)
            {
                ForwardToPeers(senderId, bytes);
            }

            OnActionReceived?.Invoke(bytes);
        }

        public void SendAction(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            if (Status != NetworkStatus.Connected) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;

            using var writer = new FastBufferWriter(data.Length, Allocator.Temp);
            writer.WriteBytesSafe(data, data.Length);

            if (IsHost)
            {
                _broadcastBuffer.Clear();
                foreach (var id in nm.ConnectedClientsIds)
                {
                    if (id == nm.LocalClientId) continue;
                    _broadcastBuffer.Add(id);
                }
                if (_broadcastBuffer.Count > 0)
                {
                    nm.CustomMessagingManager.SendNamedMessage(MessageName, _broadcastBuffer, writer);
                }
            }
            else
            {
                nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer);
            }
        }

        private void ForwardToPeers(ulong senderId, byte[] bytes)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;

            using var writer = new FastBufferWriter(bytes.Length, Allocator.Temp);
            writer.WriteBytesSafe(bytes, bytes.Length);

            _broadcastBuffer.Clear();
            foreach (var id in nm.ConnectedClientsIds)
            {
                if (id == nm.LocalClientId) continue;
                if (id == senderId) continue;
                _broadcastBuffer.Add(id);
            }

            if (_broadcastBuffer.Count > 0)
            {
                nm.CustomMessagingManager.SendNamedMessage(MessageName, _broadcastBuffer, writer);
            }
        }

        private void OnDisable()
        {
            // Cancel any in-flight bootstrap so its remaining await steps no-op.
            try { _bootstrapCts?.Cancel(); _bootstrapCts?.Dispose(); } catch { }
            _bootstrapCts = null;

            UnregisterMessageHandler();

            // Detach NGO callbacks BEFORE Shutdown so we don't react to teardown events.
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnClientConnectedCallback -= HandleClientConnected;
                nm.OnClientDisconnectCallback -= HandleClientDisconnected;
                nm.OnTransportFailure -= HandleTransportFailure;

                if (nm.IsListening || nm.IsHost || nm.IsServer || nm.IsConnectedClient)
                {
                    try { nm.Shutdown(true); }
                    catch (Exception e) { Debug.LogWarning($"[UgsNetworkTransport] Shutdown threw: {e.Message}"); }
                }
            }

            // Hand the LeaveAsync over to the static coordinator so the next transport awaits it.
            // We can't await here (OnDisable is sync), but the next bootstrap WILL await this Task
            // before recreating a session, which prevents the "second match fails to host" race.
            var session = _session;
            _session = null;
            s_PreviousShutdownTask = LeaveSessionAsync(session);

            SetStatus(NetworkStatus.Disconnected);
            _disposed = true;
        }

        private static async Task LeaveSessionAsync(ISession session)
        {
            if (session == null) return;
            try { await session.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning($"[UgsNetworkTransport] LeaveAsync threw: {e.Message}"); }
        }

        // Diagnostic: emits each char with its hex codepoint so we can see whether the host's
        // displayed string actually consists of digits, letters, or look-alike Unicode glyphs
        // (e.g. fullwidth digits or smallcaps letters that render like ASCII digits).
        private static string DescribeCodeBytes(string code)
        {
            if (string.IsNullOrEmpty(code)) return "<empty>";
            var sb = new System.Text.StringBuilder(code.Length * 8);
            for (int i = 0; i < code.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(code[i]).Append('=').Append("U+").Append(((int)code[i]).ToString("X4"));
            }
            return sb.ToString();
        }

        // Returns a unique-per-instance Unity Authentication profile name so two instances on the
        // same machine (Editor + standalone build, or two builds) don't share a cached anonymous
        // identity. Override priority:
        //   1. Command-line argument: -ugsProfile <name>           (highest -- repeatable tests)
        //   2. Environment variable:  UGS_PROFILE=<name>           (CI / shell launchers)
        //   3. Auto: "editor" in the Editor, otherwise a per-process random tag in builds.
        private static string s_CachedProfile;
        private static string GetAuthProfile()
        {
            if (!string.IsNullOrEmpty(s_CachedProfile)) return s_CachedProfile;

            string fromArg = ParseCommandLineProfile();
            if (!string.IsNullOrEmpty(fromArg)) { s_CachedProfile = fromArg; return s_CachedProfile; }

            string fromEnv = Environment.GetEnvironmentVariable("UGS_PROFILE");
            if (!string.IsNullOrEmpty(fromEnv)) { s_CachedProfile = fromEnv; return s_CachedProfile; }

#if UNITY_EDITOR
            s_CachedProfile = "editor";
#else
            // Stable for the lifetime of the process, unique across simultaneous builds.
            s_CachedProfile = "build_" + System.Diagnostics.Process.GetCurrentProcess().Id.ToString("X");
#endif
            return s_CachedProfile;
        }

        private static string ParseCommandLineProfile()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], "-ugsProfile", StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private void SetStatus(NetworkStatus next)
        {
            if (Status == next) return;
            Status = next;
            OnStatusChanged?.Invoke(next);
        }
    }
}
