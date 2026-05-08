using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using DiceGame.Core.Models;

namespace DiceGame.UI.MainMenu
{
    public class MainMenu : MonoBehaviour
    {
        [Header("UI Sliding")]
        [SerializeField] private RectTransform _panelsContainer;
        [SerializeField] private float _slideSpeed = 10f;
        private Vector2 _targetPosition;

        [Header("Panel Positionen (X-Werte)")]
        [SerializeField] private float _mainMenuX = 0f;
        [SerializeField] private float _localSetupMenuX = -1500f;

        [Header("Buttons - Main Menu")]
        [SerializeField] private Button _singleplayerButton;
        [SerializeField] private Button _multiplayerMenuButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private TextMeshProUGUI _mainMenuHighScoreText;
        [SerializeField] private Button _backFromSettingsButton;
        [SerializeField] private Button _backFromSettingsButton_1;

        [Header("Buttons - Multiplayer Auswahl")]
        [SerializeField] private Button _localButton;
        [SerializeField] private Button _onlineButton;
        [SerializeField] private Button _privateOnlineButton;
        [SerializeField] private Button _backToMainButton;

        [Header("Multiplayer Setup (Lokal)")]
        [SerializeField] private TMP_InputField[] _playerNameInputs; 
        [SerializeField] private Button _addPlayerButton;    
        [SerializeField] private Button _removePlayerButton; 
        [SerializeField] private TextMeshProUGUI _playerCountText;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _backToTypeMenuButton;

        [Header("Online Multiplayer Setup (Phase 2B)")]
        [SerializeField] private GameObject _onlinePanel;
        [SerializeField] private Button _hostOnlineButton;
        [SerializeField] private Button _joinOnlineButton;
        [SerializeField] private TMP_InputField _joinCodeInput;
        [SerializeField] private TMP_InputField _onlineLocalNameInput;
        [SerializeField] private Button _onlineBackButton;

        [Header("UI Panels")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _mainMenuPanel;
        [SerializeField] private GameObject _multiplayerTypePanel;

        [Header("Animations")]
        [SerializeField] private Animator _openSettingsAnimator; 
        [SerializeField] private Animator _settingsAnimator; 
        [SerializeField] private Animator _multiplayerTypeAnimator; 
        [SerializeField] private Animator _startLocalGameButtonAnimator;

        private int _currentPlayerCount = 1; 
        private const int MIN_PLAYERS = 1;
        private const int MAX_PLAYERS = 4;

        // Unity Lobby join codes are 6 case-insensitive alphanumeric characters. The exact set the
        // server accepts isn't publicly documented and varies between SDK versions; we accept any
        // ASCII digit (0-9) or uppercase letter (A-Z) here and let the server reject invalid codes.
        // Any rejection surfaces as SessionException("contains an invalid character 'X' (U+xxxx)").
        private const int LOBBY_CODE_LENGTH = 6;
        private bool _isFilteringJoinCode;

        private void Start()
        {
            _targetPosition = new Vector2(_mainMenuX, 0);

            if (_mainMenuHighScoreText != null)
            {
                int highScore = PlayerPrefs.GetInt("HighScore", 0);
                _mainMenuHighScoreText.text = $"High Score: {highScore}";
            }

            // Stelle sicher, dass beim Start nur das MainMenu aktiv ist
            _settingsPanel.SetActive(false); 
            _mainMenuPanel.SetActive(true);
            
            // --- Events simpel und direkt verknüpfen ---
            if (_singleplayerButton) _singleplayerButton.onClick.AddListener(StartSingleplayer);
            if (_multiplayerMenuButton)
            {
                _multiplayerMenuButton.onClick.RemoveListener(MultiplayerTypeMenu);
                _multiplayerMenuButton.onClick.AddListener(MultiplayerTypeMenu);
            }
            
            // Settings Logik
            if (_settingsButton) _settingsButton.onClick.AddListener(OpenSettings);
            if (_backFromSettingsButton) _backFromSettingsButton.onClick.AddListener(() => StartCoroutine(CloseSettings()));
            if (_backFromSettingsButton_1) _backFromSettingsButton_1.onClick.AddListener(() => StartCoroutine(CloseSettings()));
            if (_localButton) _localButton.onClick.AddListener(() => MoveTo(_localSetupMenuX));
            if (_onlineButton) _onlineButton.onClick.AddListener(OpenOnlinePanel);
            if (_privateOnlineButton) _privateOnlineButton.onClick.AddListener(OpenOnlinePanel);
            if (_backToMainButton) _backToMainButton.onClick.AddListener(() => MoveTo(_mainMenuX));

            if (_hostOnlineButton) _hostOnlineButton.onClick.AddListener(StartOnlineHost);
            if (_joinOnlineButton) _joinOnlineButton.onClick.AddListener(StartOnlineJoin);
            if (_onlineBackButton) _onlineBackButton.onClick.AddListener(CloseOnlinePanel);
            if (_joinCodeInput != null)
            {
                _joinCodeInput.characterLimit = LOBBY_CODE_LENGTH;
                _joinCodeInput.onValueChanged.AddListener(OnJoinCodeChanged);
            }
            if (_onlinePanel != null) _onlinePanel.SetActive(false);
            ValidateJoinCode();
            
            if (_startGameButton) _startGameButton.onClick.AddListener(StartLocalMultiplayer);
            
            if (_addPlayerButton) _addPlayerButton.onClick.AddListener(AddPlayer);
            if (_removePlayerButton) _removePlayerButton.onClick.AddListener(RemovePlayer);
            if (_backToTypeMenuButton) _backToTypeMenuButton.onClick.AddListener(() => MoveTo(_mainMenuX));

            foreach (var input in _playerNameInputs)
            {
                input.onValueChanged.AddListener(_ => ValidateInputs());
            }

            // Initiale UI-Aktualisierung
            UpdatePlayerCountUI();
        }

        private void Update()
        {
            if (_panelsContainer)
            {
                _panelsContainer.anchoredPosition = Vector2.Lerp(
                    _panelsContainer.anchoredPosition, 
                    _targetPosition, 
                    Time.deltaTime * _slideSpeed
                );
            }
        }

        // --- UI Methoden ---

        private void OpenSettings()
        {
            _openSettingsAnimator.SetTrigger("Pressed"); 
            _settingsPanel.SetActive(true);
            _settingsAnimator.SetBool("IsVisible", true); 
        }

        private IEnumerator CloseSettings()
        {
            _settingsAnimator.SetBool("IsVisible", false);
            yield return new WaitForSeconds(0.5f);
            _settingsPanel.SetActive(false);
        }

        private void MultiplayerTypeMenu()
        {
            bool isVisible = _multiplayerTypeAnimator.GetBool("IsVisible");
            _multiplayerTypeAnimator.SetBool("IsVisible", !isVisible);
        }

        private void MoveTo(float targetX)
        {
            _targetPosition = new Vector2(targetX, _panelsContainer.anchoredPosition.y);
        }

        // --- Logik Methoden ---

        private void AddPlayer()
        {
            if (_currentPlayerCount < MAX_PLAYERS)
            {
                _currentPlayerCount++;
                UpdatePlayerCountUI();
            }
        }

        private void RemovePlayer()
        {
            if (_currentPlayerCount > MIN_PLAYERS)
            {
                _currentPlayerCount--;
                UpdatePlayerCountUI();
            }
        }

        private void UpdatePlayerCountUI()
        {
            for (int i = 0; i < _playerNameInputs.Length; i++)
            {
                if (_playerNameInputs[i] == null) continue;

                // Wenn _currentPlayerCount == 1 ist, brauchen wir 2 Felder (Spieler + Bot).
                // Ansonsten brauchen wir exakt so viele Felder wie _currentPlayerCount.
                bool shouldBeActive = (_currentPlayerCount == 1) ? (i < 2) : (i < _currentPlayerCount);
                _playerNameInputs[i].gameObject.SetActive(shouldBeActive);

                // Spezifische Logik für das zweite Eingabefeld (Index 1)
                if (i == 1)
                {
                    if (_currentPlayerCount == 1)
                    {
                        // Bot-Modus einrichten
                        _playerNameInputs[i].text = "Bot";
                        _playerNameInputs[i].interactable = false;
                    }
                    else
                    {
                        // Multiplayer-Modus: Feld entsperren
                        _playerNameInputs[i].interactable = true;
                        
                        // Bot-Text entfernen, falls noch vorhanden
                        if (_playerNameInputs[i].text == "Bot")
                        {
                            _playerNameInputs[i].text = string.Empty;
                        }
                    }
                }
            }

            if (_addPlayerButton) _addPlayerButton.interactable = _currentPlayerCount < MAX_PLAYERS;
            if (_removePlayerButton) _removePlayerButton.interactable = _currentPlayerCount > MIN_PLAYERS;

            if (_playerCountText != null)
            {
                _playerCountText.text = (_currentPlayerCount == 1) ? "You vs <color=red>Bot</color>" : $"{_currentPlayerCount} Players";
            }

            ValidateInputs();
        }

        private void ValidateInputs()
        {
            bool allNamesEntered = true;

            // Prüft nur die echten Spieler.
            // Da bei 1 Spieler der zweite Platz vom Bot belegt ist (und _currentPlayerCount 1 ist),
            // wird das deaktivierte Bot-Feld hier richtigerweise ignoriert.
            for (int i = 0; i < _currentPlayerCount; i++)
            {
                if (_playerNameInputs[i] != null && string.IsNullOrWhiteSpace(_playerNameInputs[i].text))
                {
                    allNamesEntered = false;
                    break;
                }
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = allNamesEntered;
                _startLocalGameButtonAnimator.SetBool("IsReady", allNamesEntered); // NEU: Animation für den Start-Button
            }
        }

        public void StartSingleplayer()
        {
            MatchData.ResetToOffline();
            MatchData.PlayerNames = new List<string> { "Player 1" }; 
            SceneManager.LoadScene("InGameScene");
        }

        private void StartLocalMultiplayer()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < _currentPlayerCount; i++)
            {
                names.Add(_playerNameInputs[i].text.Trim());
            }

            // Die if-Abfrage hier ist perfekt, da das Bot-Feld in der UI nicht zu 
            // _currentPlayerCount zählt, es aber für das Match Data benötigt wird.
            if (_currentPlayerCount == 1) names.Add("<color=red>Bot</color>");

            MatchData.ResetToOffline();
            MatchData.PlayerNames = names;
            SceneManager.LoadScene("InGameScene");
        }

        private void OpenOnlinePanel()
        {
            if (_onlinePanel != null) _onlinePanel.SetActive(true);
            ValidateJoinCode();
        }

        private void CloseOnlinePanel()
        {
            if (_onlinePanel != null) _onlinePanel.SetActive(false);
            MoveTo(_mainMenuX);
        }

        private void OnJoinCodeChanged(string raw)
        {
            // Re-entry guard: setting input.text below re-fires onValueChanged.
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

        private static string FilterLobbyCode(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            // Lenient client-side filter: keep only ASCII digits and uppercased letters, clamp to
            // LOBBY_CODE_LENGTH. Whitespace, punctuation, and TMP's invisible control characters get
            // dropped so paste-with-formatting and lowercase typing both Just Work. Server-side
            // validation has the final say on which alphanumerics are actually valid.
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

        private void ValidateJoinCode()
        {
            if (_joinOnlineButton == null) return;
            bool hasFullCode = _joinCodeInput != null
                && !string.IsNullOrWhiteSpace(_joinCodeInput.text)
                && _joinCodeInput.text.Length == LOBBY_CODE_LENGTH;
            _joinOnlineButton.interactable = hasFullCode;
        }

        private string ResolveOnlineLocalName(string fallback)
        {
            if (_onlineLocalNameInput != null && !string.IsNullOrWhiteSpace(_onlineLocalNameInput.text))
            {
                return _onlineLocalNameInput.text.Trim();
            }
            if (_playerNameInputs != null && _playerNameInputs.Length > 0
                && _playerNameInputs[0] != null
                && !string.IsNullOrWhiteSpace(_playerNameInputs[0].text))
            {
                return _playerNameInputs[0].text.Trim();
            }
            return fallback;
        }

        // Phase 2B: production online flow. UgsNetworkTransport creates a Relay-backed session and
        // surfaces the join code through an in-game overlay; the client enters that code below.
        public void StartOnlineHost()
        {
            MatchData.ResetToOffline();
            MatchData.IsOnline = true;
            MatchData.UseRelay = true;
            MatchData.IsHost = true;
            MatchData.LocalPlayerId = 0;

            string localName = ResolveOnlineLocalName("Host");
            MatchData.PlayerNames = new List<string> { localName, "Remote" };
            MatchData.IsRemoteFlags = new List<bool> { false, true };

            SceneManager.LoadScene("InGameScene");
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // Editor-only smoke test: drives the legacy LocalLoopbackTransport + FakeRemotePeer flow
        // (IsOnline = true, UseRelay = false) so we can verify the lockstep pipeline without UGS.
        // Right-click the MainMenu component in the Inspector to invoke.
        [ContextMenu("Start Loopback Test (UseRelay = false)")]
        private void StartLoopbackSmokeTest()
        {
            MatchData.ResetToOffline();
            MatchData.IsOnline = true;
            MatchData.UseRelay = false;
            MatchData.IsHost = true;
            MatchData.LocalPlayerId = 0;
            MatchData.PlayerNames = new List<string> { "You", "Remote" };
            MatchData.IsRemoteFlags = new List<bool> { false, true };
            SceneManager.LoadScene("InGameScene");
        }
#endif

        public void StartOnlineJoin()
        {
            string code = _joinCodeInput != null ? FilterLobbyCode(_joinCodeInput.text) : null;
            if (string.IsNullOrWhiteSpace(code) || code.Length != LOBBY_CODE_LENGTH)
            {
                Debug.LogWarning($"[MainMenu] Invalid join code; expected {LOBBY_CODE_LENGTH} alphanumeric characters.");
                return;
            }

            MatchData.ResetToOffline();
            MatchData.IsOnline = true;
            MatchData.UseRelay = true;
            MatchData.IsHost = false;
            MatchData.LocalPlayerId = 1;
            MatchData.RelayJoinCode = code;

            string localName = ResolveOnlineLocalName("Guest");
            MatchData.PlayerNames = new List<string> { "Host", localName };
            MatchData.IsRemoteFlags = new List<bool> { true, false };

            SceneManager.LoadScene("InGameScene");
        }
    }
}