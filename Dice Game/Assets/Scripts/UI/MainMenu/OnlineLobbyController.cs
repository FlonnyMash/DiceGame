using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
using DiceGame.Core.Models;
using DiceGame.Core.Networking;
using DiceGame.Infrastructure.Networking.Ugs;
using DiceGame.Services;
using DiceGame.UI.Views;

namespace DiceGame.UI.MainMenu
{
    // Owns the entire 4-player online lobby state machine inside MainMenuScene.
    //
    // Flow summary:
    //   Choice panel -> [Host or Join with Code]
    //   Host:  spawn UgsNetworkTransport on persistent NetworkManager, show join code, accept
    //          incoming Identify packets, populate slots, "Start Match" broadcasts StartMatch
    //          and loads InGameScene.
    //   Guest: spawn transport with code, on Connected send our Identify, mirror StartMatch
    //          slots, on StartMatch arrival -> resolve our PlayerId, populate MatchData, load
    //          InGameScene.
    //
    // Lockstep constraint: the only wire packets we use are the ones declared in
    // PlayerActionType (Identify / StartMatch). Everything else stays untouched.
    public class OnlineLobbyController : MonoBehaviour
    {
        private const int LOBBY_CODE_LENGTH = 6;
        private const int MaxPlayers = StartMatchPacket.MaxPlayers;

        [Header("Choice Panel")]
        [SerializeField] private GameObject _choicePanel;
        [SerializeField] private Button _hostButton;
        [SerializeField] private Button _joinButton;
        [SerializeField] private TMP_InputField _joinCodeInput;
        [SerializeField] private TMP_InputField _localNameInput;
        [SerializeField] private Button _backFromChoiceButton;

        [Header("Host Panel")]
        [SerializeField] private GameObject _hostPanel;
        [SerializeField] private TextMeshProUGUI _joinCodeLabel;
        [SerializeField] private TextMeshProUGUI[] _hostSlotLabels = new TextMeshProUGUI[MaxPlayers];
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _backFromHostButton;

        [Header("Guest Panel")]
        [SerializeField] private GameObject _guestPanel;
        [SerializeField] private TextMeshProUGUI _guestStatusLabel;
        [SerializeField] private TextMeshProUGUI[] _guestSlotLabels = new TextMeshProUGUI[MaxPlayers];
        [SerializeField] private Button _backFromGuestButton;

        [Header("Status Overlay")]
        [SerializeField] private ConnectionStatusOverlay _statusOverlay;

        public event Action OnLobbyClosed;

        private UgsNetworkTransport _transport;
        private bool _isHost;
        private int _localClientToken;
        private string _localName = "Player";
        private bool _hasBeenConnected;
        private bool _isFilteringJoinCode;

        // Host roster: ClientToken -> (assignedPlayerId, name). Slot 0 is always the host itself.
        private readonly Dictionary<int, RosterEntry> _hostRoster = new Dictionary<int, RosterEntry>();

        // Guest mirror: latest seen rosters from incoming Identify packets (so we can show other
        // peers in the slot list before the host clicks Start). Indexed by ClientToken.
        private readonly Dictionary<int, string> _guestSeen = new Dictionary<int, string>();

        // Guest lobby only: Identify from Netcode ServerClientId fills these (slot 0 = host).
        private string _hostLobbyName;
        private int? _hostClientToken;

        public bool IsActive => _transport != null;

        private void Awake()
        {
            HideAllPanels();

            if (_hostButton != null) _hostButton.onClick.AddListener(BeginHostFlow);
            if (_joinButton != null) _joinButton.onClick.AddListener(HandleJoinClicked);
            if (_startGameButton != null) _startGameButton.onClick.AddListener(HandleStartGameClicked);

            if (_backFromChoiceButton != null) _backFromChoiceButton.onClick.AddListener(CloseLobby);
            if (_backFromHostButton != null) _backFromHostButton.onClick.AddListener(AbortAndClose);
            if (_backFromGuestButton != null) _backFromGuestButton.onClick.AddListener(AbortAndClose);

            if (_joinCodeInput != null)
            {
                _joinCodeInput.characterLimit = LOBBY_CODE_LENGTH;
                _joinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);
            }

            ValidateJoinCode();

            if (_statusOverlay != null)
            {
                _statusOverlay.OnBackToMenuClicked += AbortAndClose;
            }
        }

        private void OnDestroy()
        {
            DetachTransport();
            if (_statusOverlay != null)
            {
                _statusOverlay.OnBackToMenuClicked -= AbortAndClose;
            }
        }

        // --- public entry points ----------------------------------------------------------

        public void OpenChoice()
        {
            HideOverlay();
            HideAllPanels();
            if (_choicePanel != null) _choicePanel.SetActive(true);
            ValidateJoinCode();
        }

        public void CloseLobby()
        {
            HideOverlay();
            DetachTransport();
            HideAllPanels();
            MatchData.ResetToOffline();
            OnLobbyClosed?.Invoke();
        }

        // --- choice handlers --------------------------------------------------------------

        private void BeginHostFlow()
        {
            _isHost = true;
            _localName = ResolveLocalName("Host");
            _localClientToken = GenerateClientToken();
            _hostRoster.Clear();

            HideAllPanels();
            if (_hostPanel != null) _hostPanel.SetActive(true);
            ResetHostSlotLabels();
            _hostSlotLabels[0].text = _localName + " (You)";
            UpdateStartGameInteractable();
            if (_joinCodeLabel != null) _joinCodeLabel.text = LocalizationService.Instance.GetText("msg_connecting");

            ShowOverlay("msg_connecting");

            SpawnTransportAsHost();
        }

        private void HandleJoinClicked()
        {
            if (_joinCodeInput == null) return;
            string code = FilterLobbyCode(_joinCodeInput.text);
            if (string.IsNullOrEmpty(code) || code.Length != LOBBY_CODE_LENGTH) return;

            _isHost = false;
            _localName = ResolveLocalName("Guest");
            _localClientToken = GenerateClientToken();
            _guestSeen.Clear();
            _hostLobbyName = null;
            _hostClientToken = null;

            HideAllPanels();
            if (_guestPanel != null) _guestPanel.SetActive(true);
            ResetGuestSlotLabels();
            if (_guestStatusLabel != null)
            {
                _guestStatusLabel.text = LocalizationService.Instance.GetText("msg_connecting");
            }

            ShowOverlay("msg_connecting");

            SpawnTransportAsGuest(code);
        }

        // --- transport bootstrap ----------------------------------------------------------

        private void SpawnTransportAsHost()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[OnlineLobbyController] No NetworkManager singleton in scene. Cannot host.");
                ShowTerminalOverlay("err_connection_lost");
                return;
            }

            _transport = nm.gameObject.AddComponent<UgsNetworkTransport>();
            _transport.Configure(localPlayerId: 0, isHost: true, maxPlayers: MaxPlayers, joinCodeForClient: null);
            AttachTransportEvents();
        }

        private void SpawnTransportAsGuest(string code)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[OnlineLobbyController] No NetworkManager singleton in scene. Cannot join.");
                ShowTerminalOverlay("err_connection_lost");
                return;
            }

            _transport = nm.gameObject.AddComponent<UgsNetworkTransport>();
            // localPlayerId is provisional: the host assigns the real one via StartMatch and we
            // overwrite _transport.LocalPlayerId before LoadScene("InGameScene").
            _transport.Configure(localPlayerId: -1, isHost: false, maxPlayers: MaxPlayers, joinCodeForClient: code);
            AttachTransportEvents();
        }

        private void AttachTransportEvents()
        {
            if (_transport == null) return;
            _transport.OnJoinCodeReady += HandleJoinCodeReady;
            _transport.OnStatusChanged += HandleTransportStatusChanged;
            _transport.OnPeerConnected += HandlePeerConnected;
            _transport.OnPeerDisconnected += HandlePeerDisconnected;
            _transport.OnActionReceived += HandleActionReceived;
        }

        private void DetachTransport()
        {
            if (_transport != null)
            {
                _transport.OnJoinCodeReady -= HandleJoinCodeReady;
                _transport.OnStatusChanged -= HandleTransportStatusChanged;
                _transport.OnPeerConnected -= HandlePeerConnected;
                _transport.OnPeerDisconnected -= HandlePeerDisconnected;
                _transport.OnActionReceived -= HandleActionReceived;

                // Destroy the component so OnDisable runs LeaveAsync + nm.Shutdown.
                Destroy(_transport);
                _transport = null;
            }
            _hasBeenConnected = false;
            _hostRoster.Clear();
            _guestSeen.Clear();
            _hostLobbyName = null;
            _hostClientToken = null;
        }

        // --- transport callbacks ----------------------------------------------------------

        private void HandleJoinCodeReady(string code)
        {
            if (!_isHost) return;
            if (_joinCodeLabel != null)
            {
                _joinCodeLabel.text = LocalizationService.Instance.GetText("lobby_join_code", code);
            }
        }

        private void HandleTransportStatusChanged(NetworkStatus status)
        {
            switch (status)
            {
                case NetworkStatus.Connecting:
                case NetworkStatus.Reconnecting:
                    ShowOverlay("msg_connecting");
                    break;

                case NetworkStatus.Connected:
                    _hasBeenConnected = true;
                    HideOverlay();

                    if (_isHost)
                    {
                        // Guests map senderId == ServerClientId to the host slot; advertise our name/token.
                        BroadcastIdentify();
                        UpdateStartGameInteractable();
                    }
                    else
                    {
                        // Identify ourselves to the host (and incidentally to other guests via the
                        // host-relay forwarding path). The host turns this into a roster slot.
                        BroadcastIdentify();
                        // Our own Identify is not echoed back; refresh so "(You)" and pending host slot show immediately.
                        RefreshGuestSlotsFromMirror();
                        if (_guestStatusLabel != null)
                        {
                            _guestStatusLabel.text = LocalizationService.Instance.GetText("lobby_waiting_host");
                        }
                    }
                    break;

                case NetworkStatus.Disconnected:
                case NetworkStatus.Error:
                    if (_hasBeenConnected || status == NetworkStatus.Error)
                    {
                        ShowTerminalOverlay("err_connection_lost");
                    }
                    break;
            }
        }

        private void HandlePeerConnected(ulong _)
        {
            if (!_isHost) return;

            // Re-announce host name/token whenever a peer finishes connecting so newcomers see slot 0
            // reliably (BroadcastIdentify alone on Connected often had zero clients).
            BroadcastIdentify();

            // Roster naming still arrives via each peer's Identify packet.
            UpdateStartGameInteractable();
        }

        private void HandlePeerDisconnected(ulong _)
        {
            if (_isHost)
            {
                // We don't know which ClientToken belonged to that NGO clientId (the mapping is
                // private to the peer). The simplest robust behaviour is to drop the entry whose
                // token we no longer see in subsequent traffic. For visual snappiness on disconnect
                // we do nothing here and let the ClientToken stay until the next StartMatch attempt
                // (which only succeeds once a peer sends a fresh Identify). A more sophisticated
                // pairing of ClientToken<->NGO clientId can be added later if needed.
                UpdateStartGameInteractable();
            }
        }

        private void HandleActionReceived(byte[] data, ulong senderId)
        {
            if (data == null || data.Length == 0) return;
            if (!WireFormat.TryPeekType(data, out var type)) return;

            switch (type)
            {
                case PlayerActionType.Identify:
                    if (IdentifyPacket.TryDeserialize(data, out var ident)) HandleIdentify(ident, senderId);
                    break;
                case PlayerActionType.StartMatch:
                    if (!_isHost && StartMatchPacket.TryDeserialize(data, out var start)) HandleStartMatch(start);
                    break;
                // Anything else (Roll/ToggleHold/etc.) shouldn't be on the wire during the lobby
                // phase; we ignore silently to keep the log clean.
            }
        }

        // --- host-side: roster + start ----------------------------------------------------

        private void HandleIdentify(IdentifyPacket packet, ulong senderId)
        {
            if (_isHost)
            {
                if (packet.ClientToken == _localClientToken) return;
                if (_hostRoster.ContainsKey(packet.ClientToken))
                {
                    // Update the name in case the peer re-sent (e.g. retry).
                    var existing = _hostRoster[packet.ClientToken];
                    _hostRoster[packet.ClientToken] = new RosterEntry(packet.ClientToken, existing.AssignedPlayerId, packet.Name);
                }
                else
                {
                    byte assignedId = AssignNextHostPlayerId();
                    if (assignedId == 0) return; // lobby full
                    _hostRoster[packet.ClientToken] = new RosterEntry(packet.ClientToken, assignedId, packet.Name);
                }
                RefreshHostSlots();
                UpdateStartGameInteractable();
            }
            else
            {
                var nm = NetworkManager.Singleton;
                bool fromRelayHost = nm != null && senderId == NetworkManager.ServerClientId;

                if (fromRelayHost)
                {
                    _hostClientToken = packet.ClientToken;
                    _hostLobbyName = packet.Name;
                }
                else
                {
                    if (packet.ClientToken == _localClientToken)
                        return;
                    if (_hostClientToken.HasValue && packet.ClientToken == _hostClientToken.Value)
                        return;
                    _guestSeen[packet.ClientToken] = packet.Name;
                }

                RefreshGuestSlotsFromMirror();
            }
        }

        private byte AssignNextHostPlayerId()
        {
            // Slot 0 is the host. Assign the lowest available 1..3.
            var taken = new HashSet<byte> { 0 };
            foreach (var entry in _hostRoster.Values) taken.Add(entry.AssignedPlayerId);
            for (byte id = 1; id < MaxPlayers; id++)
            {
                if (!taken.Contains(id)) return id;
            }
            return 0; // sentinel: no slot available
        }

        private void HandleStartGameClicked()
        {
            if (!_isHost || _transport == null) return;
            if (_transport.Status != NetworkStatus.Connected) return;
            if (_hostRoster.Count == 0) return; // requires at least one guest

            var roster = BuildHostRosterForBroadcast();
            var packet = new StartMatchPacket(roster);
            _transport.SendAction(packet.Serialize());
            // Apply locally and transition (the host's own SendAction skips itself).
            ApplyStartMatchAsHost(roster);
        }

        private List<RosterEntry> BuildHostRosterForBroadcast()
        {
            var list = new List<RosterEntry>(MaxPlayers);
            list.Add(new RosterEntry(_localClientToken, 0, _localName));
            foreach (var entry in _hostRoster.Values)
            {
                list.Add(entry);
            }
            list.Sort((a, b) => a.AssignedPlayerId.CompareTo(b.AssignedPlayerId));
            return list;
        }

        private void ApplyStartMatchAsHost(IReadOnlyList<RosterEntry> roster)
        {
            PopulateMatchDataFromRoster(roster, _localClientToken);
            _transport.LocalPlayerId = MatchData.LocalPlayerId;
            // Detach our transient lobby listeners; the GameController will rebind to the same
            // persistent transport in the next scene.
            HardDetachLobbyOnly();
            SceneManager.LoadScene("InGameScene");
        }

        private void HandleStartMatch(StartMatchPacket packet)
        {
            if (_isHost) return; // hosts already applied locally
            PopulateMatchDataFromRoster(packet.Roster, _localClientToken);
            if (_transport != null) _transport.LocalPlayerId = MatchData.LocalPlayerId;
            HardDetachLobbyOnly();
            SceneManager.LoadScene("InGameScene");
        }

        private void PopulateMatchDataFromRoster(IReadOnlyList<RosterEntry> roster, int localToken)
        {
            int localId = -1;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].ClientToken == localToken)
                {
                    localId = roster[i].AssignedPlayerId;
                    break;
                }
            }
            if (localId < 0)
            {
                Debug.LogError("[OnlineLobbyController] Local ClientToken not present in StartMatch roster; aborting.");
                ShowTerminalOverlay("err_connection_lost");
                return;
            }

            var ordered = new List<RosterEntry>(roster);
            ordered.Sort((a, b) => a.AssignedPlayerId.CompareTo(b.AssignedPlayerId));

            var names = new List<string>(ordered.Count);
            var isRemote = new List<bool>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                names.Add(string.IsNullOrWhiteSpace(ordered[i].Name) ? $"Player {i + 1}" : ordered[i].Name);
                isRemote.Add(ordered[i].AssignedPlayerId != localId);
            }

            MatchData.IsOnline = true;
            MatchData.IsHost = _isHost;
            MatchData.LocalPlayerId = localId;
            MatchData.PlayerNames = names;
            MatchData.IsRemoteFlags = isRemote;
            MatchData.RelayJoinCode = _transport != null ? _transport.JoinCode : null;
        }

        // Detach our event hooks WITHOUT destroying the transport, so it can carry into
        // InGameScene where GameController rebinds.
        private void HardDetachLobbyOnly()
        {
            if (_transport == null) return;
            _transport.OnJoinCodeReady -= HandleJoinCodeReady;
            _transport.OnStatusChanged -= HandleTransportStatusChanged;
            _transport.OnPeerConnected -= HandlePeerConnected;
            _transport.OnPeerDisconnected -= HandlePeerDisconnected;
            _transport.OnActionReceived -= HandleActionReceived;
            _transport = null; // do NOT Destroy: the persistent NetworkManager owns it now
        }

        // --- guest-side: identify broadcast -----------------------------------------------

        private void BroadcastIdentify()
        {
            if (_transport == null) return;
            var packet = new IdentifyPacket(_localClientToken, _localName);
            _transport.SendAction(packet.Serialize());
        }

        // --- UI helpers -------------------------------------------------------------------

        private void HideAllPanels()
        {
            if (_choicePanel != null) _choicePanel.SetActive(false);
            if (_hostPanel != null) _hostPanel.SetActive(false);
            if (_guestPanel != null) _guestPanel.SetActive(false);
        }

        private void ResetHostSlotLabels()
        {
            string emptyText = LocalizationService.Instance.GetText("lobby_player_slot_empty");
            for (int i = 0; i < _hostSlotLabels.Length; i++)
            {
                if (_hostSlotLabels[i] != null) _hostSlotLabels[i].text = emptyText;
            }
        }

        private void ResetGuestSlotLabels()
        {
            string emptyText = LocalizationService.Instance.GetText("lobby_player_slot_empty");
            for (int i = 0; i < _guestSlotLabels.Length; i++)
            {
                if (_guestSlotLabels[i] != null) _guestSlotLabels[i].text = emptyText;
            }
        }

        private void RefreshHostSlots()
        {
            ResetHostSlotLabels();
            if (_hostSlotLabels.Length > 0 && _hostSlotLabels[0] != null)
            {
                _hostSlotLabels[0].text = _localName + " (You)";
            }
            foreach (var entry in _hostRoster.Values)
            {
                int idx = entry.AssignedPlayerId;
                if (idx >= 0 && idx < _hostSlotLabels.Length && _hostSlotLabels[idx] != null)
                {
                    _hostSlotLabels[idx].text = string.IsNullOrWhiteSpace(entry.Name) ? $"Player {idx + 1}" : entry.Name;
                }
            }
        }

        private void RefreshGuestSlotsFromMirror()
        {
            // Mirrors host roster conceptually: row 0 = host (Relay server id), rows 1..3 = joined
            // guests in stable ClientToken order. Final PlayerIds arrive in StartMatch.
            ResetGuestSlotLabels();

            string emptySlot = LocalizationService.Instance.GetText("lobby_player_slot_empty");
            if (_guestSlotLabels.Length == 0) return;

            if (_guestSlotLabels[0] != null)
            {
                if (!string.IsNullOrWhiteSpace(_hostLobbyName))
                    _guestSlotLabels[0].text = _hostLobbyName;
                else
                    _guestSlotLabels[0].text = LocalizationService.Instance.GetText("lobby_host_pending");
            }

            var entries = new List<(int token, string name)>(_guestSeen.Count + 1)
            {
                (_localClientToken, _localName)
            };

            foreach (var kvp in _guestSeen)
            {
                if (_hostClientToken.HasValue && kvp.Key == _hostClientToken.Value)
                    continue;
                entries.Add((kvp.Key, kvp.Value));
            }

            entries.Sort((a, b) => a.token.CompareTo(b.token));

            int slotIndex = 1;
            foreach (var e in entries)
            {
                if (slotIndex >= _guestSlotLabels.Length) break;
                string label = string.IsNullOrWhiteSpace(e.name)
                    ? $"Player {slotIndex + 1}"
                    : e.name;
                if (e.token == _localClientToken)
                    label += " (You)";
                if (_guestSlotLabels[slotIndex] != null)
                    _guestSlotLabels[slotIndex].text = label;
                slotIndex++;
            }

            while (slotIndex < _guestSlotLabels.Length && _guestSlotLabels[slotIndex] != null)
            {
                _guestSlotLabels[slotIndex].text = emptySlot;
                slotIndex++;
            }
        }

        private void UpdateStartGameInteractable()
        {
            if (_startGameButton == null) return;
            bool hasGuest = _hostRoster.Count > 0;
            bool transportReady = _transport != null && _transport.Status == NetworkStatus.Connected;
            _startGameButton.interactable = hasGuest && transportReady;
        }

        private void ShowOverlay(string locKey)
        {
            if (_statusOverlay != null) _statusOverlay.Show(locKey);
        }

        private void ShowTerminalOverlay(string locKey)
        {
            if (_statusOverlay != null) _statusOverlay.ShowTerminal(locKey);
        }

        private void HideOverlay()
        {
            if (_statusOverlay != null) _statusOverlay.Hide();
        }

        private void AbortAndClose()
        {
            CloseLobby();
        }

        // --- helpers ----------------------------------------------------------------------

        private string ResolveLocalName(string fallback)
        {
            if (_localNameInput != null && !string.IsNullOrWhiteSpace(_localNameInput.text))
            {
                return _localNameInput.text.Trim();
            }
            return fallback;
        }

        private static int GenerateClientToken()
        {
            // Non-zero, fits in 31 bits to avoid sign collisions with the wire format.
            int t;
            do { t = UnityEngine.Random.Range(1, int.MaxValue); } while (t == 0);
            return t;
        }

        private void OnJoinCodeChanged(string raw)
        {
            if (_isFilteringJoinCode || _joinCodeInput == null) return;
            string filtered = FilterLobbyCode(raw);
            if (filtered != raw)
            {
                _isFilteringJoinCode = true;
                int caret = Mathf.Min(_joinCodeInput.caretPosition, filtered.Length);
                _joinCodeInput.text = filtered;
                _joinCodeInput.caretPosition = caret;
                _isFilteringJoinCode = false;
            }
            ValidateJoinCode();
        }

        private void ValidateJoinCode()
        {
            if (_joinButton == null) return;
            bool ok = _joinCodeInput != null
                && !string.IsNullOrWhiteSpace(_joinCodeInput.text)
                && _joinCodeInput.text.Length == LOBBY_CODE_LENGTH;
            _joinButton.interactable = ok;
        }

        // Mirrors the lenient client-side filter used by the legacy MainMenu code: keep digits and
        // uppercased letters, drop everything else. The Lobby service has the final say on validity.
        private static string FilterLobbyCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            var sb = new System.Text.StringBuilder(LOBBY_CODE_LENGTH);
            for (int i = 0; i < raw.Length && sb.Length < LOBBY_CODE_LENGTH; i++)
            {
                char c = char.ToUpperInvariant(raw[i]);
                bool isDigit = c >= '0' && c <= '9';
                bool isLetter = c >= 'A' && c <= 'Z';
                if (isDigit || isLetter) sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
