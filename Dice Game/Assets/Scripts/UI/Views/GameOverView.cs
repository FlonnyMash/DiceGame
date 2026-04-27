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
        [SerializeField] private TextMeshProUGUI _multiplayerTitleText; // Für "Game Over"
        // HIER IST DIE WICHTIGE ÄNDERUNG: Ein Array für die 4 Zeilen
        [SerializeField] private LeaderboardEntry[] _leaderboardRows; 

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
                
                // Bild einschalten, wenn ein neuer Rekord aufgestellt wurde
                if (_newHighScoreImage != null) _newHighScoreImage.SetActive(true);
            }
            else
            {
                _highScoreText.text = $"Personal Best: {currentHighScore}";
                
                // Bild ausschalten, falls kein Rekord gebrochen wurde
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

            // 2. Zeilen befüllen
            for (int i = 0; i < _leaderboardRows.Length; i++)
            {
                // Wenn wir für diese Zeile einen Spieler haben
                if (i < sortedPlayers.Count)
                {
                    _leaderboardRows[i].gameObject.SetActive(true);
                    _leaderboardRows[i].SetData(i + 1, sortedPlayers[i].Name, sortedPlayers[i].ScoreCard.GrandTotal);
                }
                else
                {
                    // Wenn z.B. nur 2 Spieler spielen, verstecken wir Zeile 3 und 4
                    _leaderboardRows[i].gameObject.SetActive(false);
                }
            }

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}