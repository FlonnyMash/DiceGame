using System.Collections.Generic;
using System.Collections;
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
        [SerializeField] private PassDeviceView _passDeviceView;
        [SerializeField] private List<DieView> _dieViews;
        [SerializeField] private Button _rollButton;
        [SerializeField] private ScoreCardView _scoreCardView;
        [SerializeField] private GameOverView _gameOverView;
        [SerializeField] private TMPro.TextMeshProUGUI _currentPlayerNameText;

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

            if (_passDeviceView != null)
            {
                _passDeviceView.OnReadyClicked += HandlePlayerReady;
                _passDeviceView.Hide(); // Am Anfang verstecken
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
            // Erst normal würfeln (Daten ändern sich im Hintergrund sofort)
            bool success = _diceCup.Roll();
            if (success)
            {
                // Dann die Animation starten
                StartCoroutine(HandleRollAnimation());
            }
        }

        private IEnumerator HandleRollAnimation()
        {
            // 1. Buttons sperren
            _rollButton.interactable = false;
            // Falls du die Punktekarte auch sperren willst:
            // _scoreCardView.interactable = false; // Müsstest du in ScoreCardView bauen

            float duration = 0.6f; // Dauer des Flimmerns

            // 2. Allen Würfeln sagen, sie sollen wackeln
            foreach (var dieView in _dieViews)
            {
                // Wir holen uns den echten Endwert aus der DiceCup Logik, 
                // aber das wissen wir hier im Controller nicht direkt pro Index. 
                // Einfacher ist es, die Daten im DiceCup abzufragen:
            }
            
            // Korrektur für die Schleife (damit wir den Index haben):
            for (int i = 0; i < _diceCup.Dice.Count; i++)
            {
                // Wir übergeben den finalen Wert, damit der Würfel weiß, wo er stoppen muss.
                // Ob er wackelt, entscheidet er selbst (isHeld Check).
                _dieViews[i].AnimateRoll(_diceCup.Dice[i].Value, duration);
            }

            // 3. Der Controller wartet 0.6 Sekunden
            yield return new WaitForSeconds(duration);

            // 4. Animation fertig -> Punkte berechnen und Buttons freigeben
            UpdatePotentialScores();
            
            if (_diceCup.RollsLeft > 0)
            {
                _rollButton.interactable = true;
            }
            // _scoreCardView.interactable = true;
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
            if (_players.All(p => p.ScoreCard.IsComplete))
            {
                EndGame();
            }
            else
            {
                // Nächster Spieler
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                // Wenn es mehr als 1 Spieler gibt UND der nächste Spieler nicht der Bot ist: Overlay zeigen!
                if (_players.Count > 1 && CurrentPlayer.Name != "Bot")
                {
                    _passDeviceView.Show(CurrentPlayer.Name);
                }
                else
                {
                    // Singleplayer oder Bot: Wir können direkt weitermachen
                    HandlePlayerReady();
                }
            }
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
            
            if (_gameOverView == null) return;

            // Wir prüfen einfach die Anzahl der Spieler
            if (_players.Count == 1)
            {
                _gameOverView.ShowSinglePlayer(_players[0].ScoreCard.GrandTotal);
            }
            else
            {
                _gameOverView.ShowMultiPlayer(_players);
            }
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

        private void HandlePlayerReady()
        {
            // Verstecke das Overlay
            if (_passDeviceView != null) _passDeviceView.Hide();
            
            // Lade die Punktekarte des neuen Spielers und starte die Runde
            RefreshUIForCurrentPlayer();
            StartNewTurn();
        }

        private void RefreshUIForCurrentPlayer()
        {
            // Die Punktekarte wie gewohnt aktualisieren
            _scoreCardView.RefreshDisplay(CurrentPlayer.ScoreCard);
            
            if (_currentPlayerNameText != null)
            {
                if (_players.Count == 1)
                {
                    // --- NEU: Live Highscore Check im Singleplayer ---
                    int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
                    int currentScore = CurrentPlayer.ScoreCard.GrandTotal;

                    // Wenn der aktuelle Score den Highscore knackt (und es nicht das allererste Spiel überhaupt ist)
                    if (currentScore > currentHighScore && currentHighScore > 0)
                    {
                        _currentPlayerNameText.text = $"New Record: {currentScore}!";
                        _currentPlayerNameText.color = Color.green; // Zur Feier Grün einfärben
                    }
                    else
                    {
                        // Solange der Rekord noch nicht gebrochen wurde
                        // (Mathf.Max sorgt dafür, dass beim allerersten Spiel nicht "High Score: 0" steht, 
                        // sondern die Punkte live mitwachsen)
                        int displayScore = Mathf.Max(currentHighScore, currentScore);
                        _currentPlayerNameText.text = $"High Score: {displayScore}";
                        _currentPlayerNameText.color = Color.yellow;
                    }
                }
                else
                {
                    // Multiplayer: Zeige an, wer am Zug ist
                    _currentPlayerNameText.text = $"Turn: {CurrentPlayer.Name}";
                    _currentPlayerNameText.color = (CurrentPlayer.Name == "Bot") ? Color.red : Color.white;
                }
            }
        }

    }
}