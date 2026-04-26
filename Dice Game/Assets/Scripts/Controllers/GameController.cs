using UnityEngine.SceneManagement;
using System.Collections.Generic;
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
        [SerializeField] private GameOverView _gameOverView;
        [SerializeField] private List<DieView> _dieViews;
        [SerializeField] private Button _rollButton;
        [SerializeField] private ScoreCardView _scoreCardView; // NEU: Referenz auf unsere Punkte-UI

        // Core Models
        private DiceCup _diceCup;
        private ScoreCard _scoreCard; // NEU: Die Datenstruktur hinter den Punkten

        private void Start()
        {
            _diceCup = new DiceCup();
            _scoreCard = new ScoreCard();

            // 1. UI Initialisieren
            _scoreCardView.Initialize();

            // 2. Würfel-Events verbinden
            for (int i = 0; i < _dieViews.Count; i++)
            {
                int index = i;
                _dieViews[i].Initialize(index);
                _dieViews[i].OnDieClicked += HandleDieClicked;
                _diceCup.Dice[i].OnStateChanged += (die) => _dieViews[index].UpdateView(die.Value, die.IsHeld);
            }

            // 3. ScoreCard-Events verbinden
            _scoreCardView.OnCategoryClicked += HandleCategoryClicked;

            // 4. Roll-Button verbinden
            _rollButton.onClick.AddListener(OnRollButtonClicked);

            _gameOverView.OnRestartClicked += HandleRestart;
            _gameOverView.OnMainMenuClicked += HandleMainMenu;
            _gameOverView.Hide();
            
            // Startzustand herstellen
            StartNewTurn();
        }

        private void OnRollButtonClicked()
        {
            bool success = _diceCup.Roll();
            
            if (success)
            {
                UpdatePotentialScores(); // Zeige nach jedem Wurf an, was man bekommen WÜRDE
            }
            
            // Wenn 3x gewürfelt wurde, Button deaktivieren
            if (_diceCup.RollsLeft <= 0)
            {
                _rollButton.interactable = false;
            }
        }

        private void HandleDieClicked(int dieIndex)
        {
            // Würfel können nur gehalten werden, wenn man mindestens 1x gewürfelt hat
            if (_diceCup.RollsLeft < DiceCup.MaxRolls)
            {
                _diceCup.Dice[dieIndex].ToggleHold();
            }
        }

        private void HandleCategoryClicked(ScoreCategory category)
{
            if (_diceCup.RollsLeft == DiceCup.MaxRolls) return; 

            int points = ScoreCalculator.CalculateScore(_diceCup.Dice, category);

            if (_scoreCard.SetScore(category, points))
            {
                _scoreCardView.SetFinalScore(category, points);
                _scoreCardView.ClearAllPotentials();
                _scoreCardView.UpdateTotals(_scoreCard.UpperSectionRaw, _scoreCard.UpperSectionBonus, _scoreCard.GrandTotal);

                // PRÜFUNG: Ist das Spiel zu Ende?
                if (_scoreCard.IsComplete)
                {
                    EndGame();
                }
                else
                {
                    StartNewTurn();
                }
            }
        }

        private void UpdatePotentialScores()
        {
            // Geht alle Kategorien durch und berechnet die theoretischen Punkte für den aktuellen Wurf
            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                // Wenn das Feld noch leer ist, zeige die Vorschau an
                if (!_scoreCard.IsCategoryFilled(category))
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
            
            // Am Anfang der Runde (bevor gewürfelt wird) gibt es keine Vorschau
            _scoreCardView.ClearAllPotentials(); 
        }

        private void OnDestroy()
        {
            if (_rollButton != null) _rollButton.onClick.RemoveAllListeners();
            if (_scoreCardView != null) _scoreCardView.OnCategoryClicked -= HandleCategoryClicked;
        }

        private void EndGame()
        {
            _rollButton.interactable = false;
            _gameOverView.Show(_scoreCard.GrandTotal); // Panel mit Punkten einblenden
        }
    
        // Methode für den kompletten Neustart des Spiels
        private void HandleRestart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void HandleMainMenu()
        {
            SceneManager.LoadScene("MainMenuScene");
        }

    }
}