using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private float _multiplayerTypeMenuX = -1500f; 
        [SerializeField] private float _localSetupMenuX = -3000f;

        [Header("Buttons - Main Menu")]
        [SerializeField] private Button _singleplayerButton;
        [SerializeField] private Button _multiplayerMenuButton;
        [SerializeField] private TextMeshProUGUI _mainMenuHighScoreText;

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

        [Header("Animation Settings")]
        [SerializeField, Tooltip("Wie lange soll gewartet werden, damit man das Pressed-Image sieht?")] 
        private float _buttonPressDelay = 0.15f;

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

            // Events über die neue Wait-Coroutine verknüpfen
            if (_singleplayerButton) _singleplayerButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_singleplayerButton, StartSingleplayer)));
            if (_multiplayerMenuButton) _multiplayerMenuButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_multiplayerMenuButton, () => MoveTo(_multiplayerTypeMenuX))));
            
            if (_localButton) _localButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_localButton, () => MoveTo(_localSetupMenuX))));
            if (_backToMainButton) _backToMainButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_backToMainButton, () => MoveTo(_mainMenuX))));
            
            if (_startGameButton) _startGameButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_startGameButton, StartLocalMultiplayer)));
            if (_backToTypeMenuButton) _backToTypeMenuButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_backToTypeMenuButton, () => MoveTo(_multiplayerTypeMenuX))));
            
            if (_addPlayerButton) _addPlayerButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_addPlayerButton, AddPlayer)));
            if (_removePlayerButton) _removePlayerButton.onClick.AddListener(() => StartCoroutine(WaitAndExecute(_removePlayerButton, RemovePlayer)));

            foreach (var input in _playerNameInputs)
            {
                input.onValueChanged.AddListener(_ => ValidateInputs());
            }

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

        /// <summary>
        /// Gibt der im Unity-Editor eingestellten Button-Animation Zeit abzuspielen,
        /// bevor die eigentliche Logik ausgeführt wird.
        /// </summary>
        private IEnumerator WaitAndExecute(Button button, Action actionToExecute)
        {
            // Verhindert, dass der Spieler mehrmals auf den Button hämmert
            button.interactable = false;

            // Warte die eingestellte Zeit, damit das Pressed-Image in Ruhe angezeigt wird
            yield return new WaitForSeconds(_buttonPressDelay);

            // Re-aktiviere den Button
            button.interactable = true;

            // Führe den Screenwechsel / die Aktion aus
            actionToExecute?.Invoke();
        }

        private void MoveTo(float targetX)
        {
            _targetPosition = new Vector2(targetX, _panelsContainer.anchoredPosition.y);
        }

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
                if (_playerNameInputs[i] != null)
                    _playerNameInputs[i].gameObject.SetActive(i < _currentPlayerCount);
            }

            if (_addPlayerButton) _addPlayerButton.interactable = _currentPlayerCount < MAX_PLAYERS;
            if (_removePlayerButton) _removePlayerButton.interactable = _currentPlayerCount > MIN_PLAYERS;

            if (_playerCountText != null)
            {
                _playerCountText.text = (_currentPlayerCount == 1) ? "1 Player (vs Bot)" : $"{_currentPlayerCount} Players";
            }

            ValidateInputs();
        }

        private void ValidateInputs()
        {
            bool allNamesEntered = true;

            for (int i = 0; i < _currentPlayerCount; i++)
            {
                if (string.IsNullOrWhiteSpace(_playerNameInputs[i].text))
                {
                    allNamesEntered = false;
                    break;
                }
            }

            if (_startGameButton != null)
            {
                _startGameButton.interactable = allNamesEntered;
            }
        }

        public void StartSingleplayer()
        {
            // Damit wird die Liste geleert und NUR "Player 1" reingeschrieben
            GameSettings.PlayerNames = new List<string> { "Player 1" }; 
            SceneManager.LoadScene("InGameScene");
        }

        private void StartLocalMultiplayer()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < _currentPlayerCount; i++)
            {
                names.Add(_playerNameInputs[i].text.Trim());
            }

            if (_currentPlayerCount == 1) names.Add("Bot");

            GameSettings.PlayerNames = names;
            SceneManager.LoadScene("InGameScene");
        }
    }
}