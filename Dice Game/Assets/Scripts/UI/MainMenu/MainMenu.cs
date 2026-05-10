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

        [Header("Online Lobby (Phase 2C)")]
        [Tooltip("Owns the entire 4-player Relay lobby state machine. Required for online play.")]
        [SerializeField] private OnlineLobbyController _onlineLobby;

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
            if (_onlineButton) _onlineButton.onClick.AddListener(OpenOnlineLobby);
            if (_privateOnlineButton) _privateOnlineButton.onClick.AddListener(OpenOnlineLobby);
            if (_backToMainButton) _backToMainButton.onClick.AddListener(() => MoveTo(_mainMenuX));

            if (_onlineLobby != null)
            {
                _onlineLobby.OnLobbyClosed += HandleLobbyClosed;
            }

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

        private void OnDestroy()
        {
            if (_onlineLobby != null)
            {
                _onlineLobby.OnLobbyClosed -= HandleLobbyClosed;
            }
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

        // Entry point for the new Phase 2C lobby. The MainMenu is now agnostic about hosting vs.
        // joining vs. private codes -- all of that lives in OnlineLobbyController.
        private void OpenOnlineLobby()
        {
            if (_onlineLobby == null)
            {
                Debug.LogError("[MainMenu] _onlineLobby is not wired. Drop the OnlineLobbyController into the Inspector reference.", this);
                return;
            }
            _onlineLobby.OpenChoice();
        }

        private void HandleLobbyClosed()
        {
            // Lobby cancelled / aborted: bring the menu back to the multiplayer type screen so
            // the player can either retry or pick local play.
            MoveTo(_mainMenuX);
        }
    }
}
