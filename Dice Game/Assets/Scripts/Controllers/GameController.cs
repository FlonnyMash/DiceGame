using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;
using DiceGame.UI.Views;
using DiceGame.Core.AI;
using DiceGame.Audio;

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
        [SerializeField] private TextMeshProUGUI _multiplayerScoreTrackerText;

        [Header("UI Panels")]
        [SerializeField] private GameObject _optionsPanel;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip[] _rollDiceSounds;
        [SerializeField] private AudioClip _scoreCategorySound;

        // Core Models
        private DiceCup _diceCup;
        private List<Player> _players = new List<Player>();
        private int _currentPlayerIndex = 0;
        private bool _isEndingTurn = false;

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
            
            // Sound abspielen!
            if (_rollDiceSounds != null && _rollDiceSounds.Length > 0)
            {
                // 1. Zufälligen Clip auswählen
                int randomIndex = UnityEngine.Random.Range(0, _rollDiceSounds.Length);
                AudioClip selectedClip = _rollDiceSounds[randomIndex];

                // 2. Den Clip über den AudioManager abspielen
                DiceGame.Audio.AudioManager.Instance.PlaySFX(selectedClip);
            }

            float duration = 1.5f; // Dauer des Flimmerns

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
            if (_diceCup.RollsLeft == DiceCup.MaxRolls || _isEndingTurn) return; 

            int points = ScoreCalculator.CalculateScore(_diceCup.Dice, category);

            if (CurrentPlayer.ScoreCard.SetScore(category, points))
            {
                // Sound abspielen
                if (DiceGame.Audio.AudioManager.Instance != null)
                {
                    DiceGame.Audio.AudioManager.Instance.PlaySFX(_scoreCategorySound);
                }

                _scoreCardView.SetFinalScore(category, points);
                _scoreCardView.ClearAllPotentials();
                _scoreCardView.UpdateTotals(
                    CurrentPlayer.ScoreCard.UpperSectionRaw, 
                    CurrentPlayer.ScoreCard.UpperSectionBonus, 
                    CurrentPlayer.ScoreCard.GrandTotal
                );

                UpdateMultiplayerScoreTracker();

                // NEU: Wir übergeben der Coroutine, ob sie warten soll.
                // Wir warten nur, wenn mehr als 1 Spieler dabei ist.
                bool shouldWait = _players.Count > 1;
                StartCoroutine(EndTurnSequence(shouldWait));
            }
        }

        private System.Collections.IEnumerator EndTurnSequence(bool wait)
        {
            _isEndingTurn = true;
            _rollButton.interactable = false; 

            // Nur pausieren, wenn wir im Multiplayer sind
            if (wait)
            {
                yield return new WaitForSeconds(2.0f);
            }
            else
            {
                // Im Singleplayer nur einen ganz kurzen Moment warten (z.B. 0.2s),
                // damit das UI Zeit hat, die Zahlen anzuzeigen, bevor alles zurückgesetzt wird.
                yield return new WaitForSeconds(0.2f);
            }

            _isEndingTurn = false;
            _rollButton.interactable = true; // Button wieder freigeben
            
            CheckGameState();
        }

        private void CheckGameState()
        {
            if (_players.All(p => p.ScoreCard.IsComplete))
            {
                EndGame();
            }
            else
            {
                // Wir merken uns, wer gerade dran WAR
                int previousPlayerIndex = _currentPlayerIndex;
                
                // Wir schalten zum NÄCHSTEN Spieler um
                _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
                
                // Logik-Check:
                // Ein "Pass Device" macht nur Sinn, wenn:
                // 1. Mehr als 1 Spieler im Spiel ist.
                // 2. Der Spieler, der gerade fertig wurde, KEIN Bot war.
                // 3. Der Spieler, der jetzt dran kommt, KEIN Bot ist.
                bool wasHuman = _players[previousPlayerIndex].Name != "Bot";
                bool isNextHuman = CurrentPlayer.Name != "Bot";

                if (_players.Count > 1 && wasHuman && isNextHuman)
                {
                    _passDeviceView.Show(CurrentPlayer.Name);
                }
                else
                {
                    // Wenn ein Bot im Spiel ist oder es Singleplayer ist, 
                    // geht es sofort ohne Overlay weiter.
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
            _scoreCardView.ClearAllPotentials(); 

            UpdateMultiplayerScoreTracker();
            
            // NEU: Bot-Weiche
            if (CurrentPlayer.Name == "Bot")
            {
                // Bot ist dran: UI sperren und Bot-Routine starten
                _rollButton.interactable = false;
                StartCoroutine(RunBotTurn());
            }
            else
            {
                // Mensch ist dran: Button freigeben
                _rollButton.interactable = true;
            }
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

        private System.Collections.IEnumerator RunBotTurn()
        {
            // Der Bot würfelt 2 bis 3 Mal (damit es nicht immer vorhersehbar ist)
            int rollsToDo = UnityEngine.Random.Range(2, 4);

            for (int r = 0; r < rollsToDo; r++)
            {
                // 1. Kurze Denkpause vor dem Würfeln
                yield return new WaitForSeconds(1.0f);

                // 2. Bot würfelt (nutzt unsere bestehende Würfel-Logik & Animation!)
                _diceCup.Roll();
                yield return StartCoroutine(HandleRollAnimation());

                // 3. Wenn es nicht der letzte Wurf ist, entscheidet der Bot, was er behält
                if (r < rollsToDo - 1)
                {
                    yield return new WaitForSeconds(0.8f); // Bot schaut sich die Würfel an
                    
                    List<int> diceToHold = BotLogic.GetDiceToHold(_diceCup.Dice);
                    
                    // Jeden ausgewählten Würfel einzeln antippen (mit kurzer Verzögerung wie ein Mensch)
                    foreach (int index in diceToHold)
                    {
                        if (!_diceCup.Dice[index].IsHeld)
                        {
                            _diceCup.Dice[index].ToggleHold();
                            _dieViews[index].UpdateView(_diceCup.Dice[index].Value, true);
                            yield return new WaitForSeconds(0.3f);
                        }
                    }
                }
            }

            // 4. Letzte Denkpause, bevor er die Punkte einträgt
            yield return new WaitForSeconds(1.2f);

            // 5. Bot fragt sein Gehirn nach der besten Kategorie
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(CurrentPlayer.ScoreCard, _diceCup.Dice);
            
            // 6. Bot simuliert den Klick auf die Kategorie!
            HandleCategoryClicked(chosenCategory); 
        }

        private void UpdateMultiplayerScoreTracker()
        {
            // Wenn das Textfeld nicht verknüpft ist oder wir im reinen Singleplayer sind, 
            // machen wir das Feld unsichtbar.
            if (_multiplayerScoreTrackerText == null) return;

            if (_players.Count <= 1)
            {
                _multiplayerScoreTrackerText.gameObject.SetActive(false);
                return;
            }

            _multiplayerScoreTrackerText.gameObject.SetActive(true);

            // Wir bauen den Text zusammen
            string trackerString = "";
            for (int i = 0; i < _players.Count; i++)
            {
                trackerString += $"{_players[i].Name}: {_players[i].ScoreCard.GrandTotal}";
                
                // Füge den Trennstrich hinzu (außer nach dem letzten Spieler)
                if (i < _players.Count - 1)
                {
                    trackerString += "   |   ";
                }
            }

            _multiplayerScoreTrackerText.text = trackerString;
        }

        // Wird vom Zahnrad-Button aufgerufen
        public void OpenOptions()
        {
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(true);
                
                // Optional: Einen Klick-Sound abspielen
                if (DiceGame.Audio.AudioManager.Instance != null)
                {
                    // AudioManager.Instance.PlaySFX(_scoreCategorySound); // oder einen eigenen Klick-Sound
                }
            }
        }

        // Wird vom "Weiterspielen"-Button aufgerufen
        public void CloseOptions()
        {
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(false);
            }
        }

        // Wird vom "Hauptmenü"-Button aufgerufen
        public void GoToMainMenu()
        {
            // Lade die Main Menu Szene. 
            // ACHTUNG: Der Name hier muss EXAKT so lauten wie deine Szene im Projekt!
            SceneManager.LoadScene("MainMenuScene"); 
        }

    }
}