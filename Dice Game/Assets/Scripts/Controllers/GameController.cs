using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;
using DiceGame.UI.Views;

namespace DiceGame.Controllers
{
    public class GameController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private List<DieView> _dieViews;
        [SerializeField] private Button _rollButton;
        [SerializeField] private ScoreCardView _scoreCardView;
        [SerializeField] private GameOverView _gameOverView;

        // Core Models
        private DiceCup _diceCup;
        private List<Player> _players = new List<Player>();
        private int _currentPlayerIndex = 0;

        private Player CurrentPlayer => _players[_currentPlayerIndex];

        private void Start()
        {
            _diceCup = new DiceCup();
            
            SetupGame(GameSettings.PlayerNames);

            // Würfel-Events verbinden
            for (int i = 0; i < _dieViews.Count; i++)
            {
                int index = i;
                _dieViews[i].Initialize(index);
                _dieViews[i].OnDieClicked += HandleDieClicked;
                _diceCup.Dice[i].OnStateChanged += (die) => _dieViews[index].UpdateView(die.Value, die.IsHeld);
            }

            // ScoreCard-Events verbinden
            _scoreCardView.OnCategoryClicked += HandleCategoryClicked;

            // Roll-Button verbinden
            _rollButton.onClick.AddListener(OnRollButtonClicked);
            
            // GameOver Events verbinden
            if (_gameOverView != null)
            {
                _gameOverView.OnRestartClicked += HandleRestart;
                _gameOverView.OnMainMenuClicked += HandleMainMenu;
                _gameOverView.Hide();
            }
        }

        public void SetupGame(List<string> names)
        {
            _players.Clear();
            foreach (var name in names)
            {
                _players.Add(new Player(name));
            }
            _currentPlayerIndex = 0;
            
            _scoreCardView.Initialize(); 
            RefreshUIForCurrentPlayer();
            StartNewTurn();
        }

        private void OnRollButtonClicked()
        {
            bool success = _diceCup.Roll();
            if (success)
            {
                UpdatePotentialScores();
            }
            
            if (_diceCup.RollsLeft <= 0)
            {
                _rollButton.interactable = false;
            }
        }

        private void HandleDieClicked(int dieIndex)
        {
            if (_diceCup.RollsLeft < DiceCup.MaxRolls)
            {
                _diceCup.Dice[dieIndex].ToggleHold();
            }
        }

        private void HandleCategoryClicked(ScoreCategory category)
        {
            if (_diceCup.RollsLeft == DiceCup.MaxRolls) return; 

            int points = ScoreCalculator.CalculateScore(_diceCup.Dice, category);

            // Nutze die ScoreCard des aktuellen Spielers
            if (CurrentPlayer.ScoreCard.SetScore(category, points))
            {
                _scoreCardView.SetFinalScore(category, points);
                _scoreCardView.ClearAllPotentials();
                _scoreCardView.UpdateTotals(
                    CurrentPlayer.ScoreCard.UpperSectionRaw, 
                    CurrentPlayer.ScoreCard.UpperSectionBonus, 
                    CurrentPlayer.ScoreCard.GrandTotal
                );

                CheckGameState();
            }
        }

        private void CheckGameState()
        {
            // Prüfen, ob ALLE Spieler fertig sind (alle Felder voll)
            if (_players.All(p => p.ScoreCard.IsComplete))
            {
                EndGame();
            }
            else
            {
                // Nächster Spieler (Ringtausch)
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                if (_players.Count > 1)
                {
                    // LOGIK: Hier würde später das "Pass Device" Overlay eingeblendet
                    Debug.Log($"Nächster Spieler: {CurrentPlayer.Name}");
                }
                
                RefreshUIForCurrentPlayer();
                StartNewTurn();
            }
        }

        private void RefreshUIForCurrentPlayer()
        {
            _scoreCardView.RefreshDisplay(CurrentPlayer.ScoreCard);
            // Optional: UI-Text für den aktuellen Namen aktualisieren
        }

        private void UpdatePotentialScores()
        {
            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                if (!CurrentPlayer.ScoreCard.IsCategoryFilled(category))
                {
                    int potentialScore = ScoreCalculator.CalculateScore(_diceCup.Dice, category);
                    _scoreCardView.ShowPotentialScore(category, potentialScore);
                }
            }
        }

        private void StartNewTurn()
        {
            _diceCup.ResetTurn();
            _rollButton.interactable = true;
            _scoreCardView.ClearAllPotentials(); 
        }

        private void EndGame()
        {
            _rollButton.interactable = false;
            if (_gameOverView != null)
                _gameOverView.Show(CurrentPlayer.ScoreCard.GrandTotal);
        }

        private void HandleRestart()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void HandleMainMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
        }

        private void OnDestroy()
        {
            if (_rollButton != null) _rollButton.onClick.RemoveAllListeners();
            if (_scoreCardView != null) _scoreCardView.OnCategoryClicked -= HandleCategoryClicked;
            if (_gameOverView != null)
            {
                _gameOverView.OnRestartClicked -= HandleRestart;
                _gameOverView.OnMainMenuClicked -= HandleMainMenu;
            }
        }
    }
}