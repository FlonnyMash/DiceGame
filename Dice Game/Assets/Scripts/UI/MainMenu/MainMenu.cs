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
        [SerializeField] private RectTransform _panelsContainer; // Der "Waggon", in dem alle Panels liegen
        [SerializeField] private float _slideSpeed = 10f; // Wie schnell die "Kamera" fährt
        private Vector2 _targetPosition;

        [Header("Panel Positionen (X-Werte)")]
        // Diese Werte geben an, wohin der Container geschoben werden muss.
        // Wenn dein Canvas z.B. 1080 Pixel breit ist, nimmst du Vielfache davon.
        [SerializeField] private float _mainMenuX = 0f;
        [SerializeField] private float _multiplayerTypeMenuX = -1500f; 
        [SerializeField] private float _localSetupMenuX = -3000f;

        [Header("Buttons - Main Menu")]
        [SerializeField] private Button _singleplayerButton;
        [SerializeField] private Button _multiplayerMenuButton;

        [Header("Buttons - Multiplayer Auswahl")]
        [SerializeField] private Button _localButton;
        [SerializeField] private Button _onlineButton; // Später
        [SerializeField] private Button _privateOnlineButton; // Später
        [SerializeField] private Button _backToMainButton;

        [Header("Multiplayer Setup (Lokal)")]
        [SerializeField] private TMP_InputField[] _playerNameInputs;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _backToTypeMenuButton;

        private void Start()
        {
            // Startposition setzen
            _targetPosition = new Vector2(_mainMenuX, 0);

            // --- Events verknüpfen ---
            
            // 1. Hauptmenü
            _singleplayerButton.onClick.AddListener(StartSingleplayer);
            _multiplayerMenuButton.onClick.AddListener(() => MoveTo(_multiplayerTypeMenuX));

            // 2. Multiplayer Auswahl
            _localButton.onClick.AddListener(() => MoveTo(_localSetupMenuX));
            _backToMainButton.onClick.AddListener(() => MoveTo(_mainMenuX));
            
            // Online-Buttons vorerst deaktivieren, bis wir sie bauen
            _onlineButton.interactable = false;
            _privateOnlineButton.interactable = false;

            // 3. Lokales Setup
            _startGameButton.onClick.AddListener(StartLocalMultiplayer);
            _backToTypeMenuButton.onClick.AddListener(() => MoveTo(_multiplayerTypeMenuX));
        }

        private void Update()
        {
            // Bewegt den Container jeden Frame ein Stück näher ans Ziel (Sliding-Effekt)
            _panelsContainer.anchoredPosition = Vector2.Lerp(
                _panelsContainer.anchoredPosition, 
                _targetPosition, 
                Time.deltaTime * _slideSpeed
            );
        }

        // Hilfsmethode für das Verschieben
        private void MoveTo(float targetX)
        {
            _targetPosition = new Vector2(targetX, _panelsContainer.anchoredPosition.y);
        }

        private void StartSingleplayer()
        {
            GameSettings.PlayerNames = new List<string> { "Spieler 1" };
            SceneManager.LoadScene("InGameScene");
        }

        private void StartLocalMultiplayer()
        {
            List<string> names = new List<string>();
            foreach (var input in _playerNameInputs)
            {
                if (!string.IsNullOrWhiteSpace(input.text))
                    names.Add(input.text.Trim());
            }

            // Fallbacks, falls nichts eingetippt wurde
            if (names.Count == 0)
            {
                names.Add("Spieler 1");
                names.Add("Spieler 2");
            }
            else if (names.Count == 1)
            {
                names.Add("Spieler 2");
            }

            GameSettings.PlayerNames = names;
            SceneManager.LoadScene("InGameScene");
        }
    }
}