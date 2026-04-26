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

        private int _currentPlayerCount = 1; 
        private const int MIN_PLAYERS = 1;
        private const int MAX_PLAYERS = 4;

        private void Start()
        {
            _targetPosition = new Vector2(_mainMenuX, 0);

            // Events verknüpfen
            if (_singleplayerButton) _singleplayerButton.onClick.AddListener(StartSingleplayer);
            if (_multiplayerMenuButton) _multiplayerMenuButton.onClick.AddListener(() => MoveTo(_multiplayerTypeMenuX));
            if (_localButton) _localButton.onClick.AddListener(() => MoveTo(_localSetupMenuX));
            if (_backToMainButton) _backToMainButton.onClick.AddListener(() => MoveTo(_mainMenuX));
            if (_startGameButton) _startGameButton.onClick.AddListener(StartLocalMultiplayer);
            if (_backToTypeMenuButton) _backToTypeMenuButton.onClick.AddListener(() => MoveTo(_multiplayerTypeMenuX));
            if (_addPlayerButton) _addPlayerButton.onClick.AddListener(AddPlayer);
            if (_removePlayerButton) _removePlayerButton.onClick.AddListener(RemovePlayer);

            // NEU: Überwachung der Textfelder
            foreach (var input in _playerNameInputs)
            {
                // Jedes Mal wenn sich der Text ändert, rufen wir die Prüfung auf
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

            // Nach der UI-Änderung sofort prüfen, ob der Start-Button an sein darf
            ValidateInputs();
        }

        // NEU: Diese Methode prüft, ob alle Namen da sind
        private void ValidateInputs()
        {
            bool allNamesEntered = true;

            for (int i = 0; i < _currentPlayerCount; i++)
            {
                // Wenn ein aktives Feld leer ist (oder nur Leerzeichen hat)
                if (string.IsNullOrWhiteSpace(_playerNameInputs[i].text))
                {
                    allNamesEntered = false;
                    break;
                }
            }

            // Start-Button nur anklickbar, wenn alle Namen eingetragen sind
            if (_startGameButton != null)
            {
                _startGameButton.interactable = allNamesEntered;
            }
        }

        private void StartSingleplayer()
        {
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