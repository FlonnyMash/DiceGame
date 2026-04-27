using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Models;

namespace DiceGame.UI.Views
{
    public class GameOverView : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject _singlePlayerContent;
        [SerializeField] private GameObject _multiPlayerContent;

        [Header("Single Player Elements")]
        [SerializeField] private TextMeshProUGUI _finalScoreText;
        [SerializeField] private TextMeshProUGUI _highScoreText;
        [SerializeField] private GameObject _newHighScoreImage;

        [Header("Multiplayer Elements")]
        [SerializeField] private TextMeshProUGUI _multiplayerTitleText; 
        [SerializeField] private PlayerScoreEntry[] _playerScoreEntries; // Deine 4 Zeilen

        // NEU: Hier kommen im Unity Inspector deine Sprites rein (z.B. 3 Stück für Platz 1-3)
        [Header("Rank Sprites")]
        [SerializeField] private Sprite[] _rankSprites; 

        [Header("Common Elements")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;

        public event Action OnRestartClicked;
        public event Action OnMainMenuClicked;

        private void Awake()
        {
            if (_restartButton) _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            if (_mainMenuButton) _mainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke());
        }

        public void ShowSinglePlayer(int score)
        {
            _singlePlayerContent.SetActive(true);
            _multiPlayerContent.SetActive(false);

            _finalScoreText.text = $"Your Score: {score}";
            
            int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
            if (score > currentHighScore)
            {
                PlayerPrefs.SetInt("HighScore", score);
                _highScoreText.text = "New Personal Best!";
                if (_newHighScoreImage != null) _newHighScoreImage.SetActive(true);
            }
            else
            {
                _highScoreText.text = $"Personal Best: {currentHighScore}";
                if (_newHighScoreImage != null) _newHighScoreImage.SetActive(false);
            }

            gameObject.SetActive(true);
        }

        public void ShowMultiPlayer(List<Player> players)
        {
            _singlePlayerContent.SetActive(false);
            _multiPlayerContent.SetActive(true);

            if (_multiplayerTitleText) _multiplayerTitleText.text = "Game Over";

            // 1. Sortieren (Bester zuerst)
            var sortedPlayers = players.OrderByDescending(p => p.ScoreCard.GrandTotal).ToList();

            // 2. Zeilen befüllen (Fehler behoben: _playerScoreEntries statt _leaderboardRows genutzt)
            for (int i = 0; i < _playerScoreEntries.Length; i++)
            {
                if (i < sortedPlayers.Count)
                {
                    _playerScoreEntries[i].gameObject.SetActive(true);

                    // NEU: Das richtige Sprite anhand des Platzes (Index i) suchen
                    Sprite rankSprite = null;
                    if (i < _rankSprites.Length)
                    {
                        rankSprite = _rankSprites[i];
                    }

                    // NEU: Wir übergeben das Sprite statt der Platzierungs-Zahl an SetData
                    _playerScoreEntries[i].SetData(rankSprite, sortedPlayers[i].Name, sortedPlayers[i].ScoreCard.GrandTotal);
                }
                else
                {
                    // Zeile verstecken, wenn kein Spieler dafür da ist
                    _playerScoreEntries[i].gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}