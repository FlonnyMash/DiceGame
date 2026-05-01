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
        [SerializeField] private CanvasGroup _diceCanvasGroup;
        [SerializeField] private Button _rollButton;
        [SerializeField] private ScoreCardView _scoreCardView;
        [SerializeField] private CanvasGroup _scoreCardCanvasGroup;
        [SerializeField] private GameOverView _gameOverView;
        [SerializeField] private TMPro.TextMeshProUGUI _currentPlayerNameText;
        [SerializeField] private TextMeshProUGUI _multiplayerScoreTrackerText;
        [SerializeField] private Button _skipBotButton;
        [SerializeField] private BotController _botController;

        [Header("UI Panels")]
        [SerializeField] private GameObject _settingsPanel;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip[] _rollDiceSounds;
        [SerializeField] private AudioClip _scoreCategorySound;
        [SerializeField] private AudioClip _bonusClaimSound;

        
        // Core Models
        public List<DieView> DieViews => _dieViews;
        private DiceCup _diceCup;
        public DiceCup DiceCup => _diceCup;
        private List<Player> _players = new List<Player>();
        private int _currentPlayerIndex = 0;
        private bool _isEndingTurn = false;

        public Player CurrentPlayer => _players[_currentPlayerIndex];
        public event System.Action OnTurnStarted;



        private void Start()
        {
            _settingsPanel.SetActive(false);
            _diceCup = new DiceCup();
            
            SetupGame(MatchData.PlayerNames);

            if (_skipBotButton != null)
            {
                _skipBotButton.onClick.AddListener(() => _botController.SkipBotTurn());
            }

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
            _scoreCardView.OnBonusClaimClicked += HandleBonusClaimed;

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
            // Prüfen, ob überhaupt ein Bot mitspielt
            bool botIsPresent = names.Contains("Bot");
            
            // BotController finden und nur aktivieren, wenn nötig
            var botCtrl = Object.FindAnyObjectByType<BotController>(FindObjectsInactive.Include);
            if (botCtrl != null)
            {
                botCtrl.gameObject.SetActive(botIsPresent);
            }
        }

        public void OnRollButtonClicked()
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
            if (_diceCanvasGroup != null)
            {
                _diceCanvasGroup.blocksRaycasts = false; // Maus geht einfach durch
            }
            
            // 1. Buttons sperren
            _rollButton.interactable = false;
            
            // NEU: Wir prüfen, ob ALLE Würfel im Becher gehalten werden
            bool allDiceHeld = _diceCup.Dice.All(die => die.IsHeld);

            // Sound NUR abspielen, wenn NICHT alle gehalten werden!
            if (!allDiceHeld)
            {
                if (_rollDiceSounds != null && _rollDiceSounds.Length > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, _rollDiceSounds.Length);
                    AudioClip selectedClip = _rollDiceSounds[randomIndex];
                    DiceGame.Audio.AudioManager.Instance.PlaySFX(selectedClip);
                }
            }

            // Deine eingestellte Dauer von 1.5 Sekunden
            float duration = 1.5f; 

            // 2. Allen Würfeln sagen, sie sollen wackeln (mit Index-Schleife)
            for (int i = 0; i < _diceCup.Dice.Count; i++)
            {
                // Wir übergeben den finalen Wert, damit der Würfel weiß, wo er stoppen muss.
                // Ob er wackelt, entscheidet er selbst (isHeld Check).
                _dieViews[i].AnimateRoll(_diceCup.Dice[i].Value, duration);
            }

            // 3. Der Controller wartet auf die Animation
            yield return new WaitForSeconds(duration);

            
            // Nur freigeben, wenn noch Würfe übrig sind UND ein Mensch spielt
            if (_diceCup.RollsLeft > 0 && CurrentPlayer.Name != "Bot")
            {
                _rollButton.interactable = true;
            }

            if (_diceCanvasGroup != null)
            {
                // WICHTIG: Nur entsperren, wenn ein Mensch dran ist!
                if (CurrentPlayer.Name != "Bot")
                {
                    _diceCanvasGroup.blocksRaycasts = true;
                }
            }

            // 4. Animation fertig -> Punkte berechnen und Buttons freigeben
            UpdatePotentialScores();

        }

        private void HandleDieClicked(int dieIndex)
        {
            // Input-Ebene: Der Türsteher blockiert echte Klicks, wenn der Bot dran ist
            if (CurrentPlayer.Name == "Bot") return;
            
            // Wenn der Spieler dran ist, leiten wir die Anfrage an die zentrale Logik weiter
            ToggleDieState(dieIndex);
        }

        // --- NEU: Diese Methode kann vom Spieler UND vom Bot aufgerufen werden ---
        public void ToggleDieState(int dieIndex)
        {
            // Logik-Ebene: Darf überhaupt gehalten werden?
            if (_diceCup.RollsLeft < DiceCup.MaxRolls)
            {
                // 1. Datenmodell aktualisieren
                _diceCup.Dice[dieIndex].ToggleHold();

                // 2. Zustand abfragen
                int currentValue = _diceCup.Dice[dieIndex].Value;
                bool isNowHeld = _diceCup.Dice[dieIndex].IsHeld;

                // 3. UI synchronisieren (Bilder und Animation)
                _dieViews[dieIndex].UpdateView(currentValue, isNowHeld);
                _dieViews[dieIndex].PlayToggleAnimation(isNowHeld);
            }
        }

        public void HandleCategoryClicked(ScoreCategory category)
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

            // Button nur freigeben, wenn der aktuelle (oder nächste) Spieler kein Bot ist
            if (CurrentPlayer.Name != "Bot")
            {
                _rollButton.interactable = true; 
            }
                        
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
                    SetUIInteractable(CurrentPlayer.Name != "Bot");
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
            // 1. Datenmodell aufräumen (Würfel entsperren, Wurf-Zähler auf 0, etc.)
            _diceCup.ResetTurn();

            // --- NEU: Die Würfel-UI für den neuen Zug lautlos zurücksetzen ---
            // (Ich gehe davon aus, dass dein Array mit den Views _dieViews heißt)
            if (_dieViews != null)
            {
                for (int i = 0; i < _dieViews.Count; i++)
                {
                    // Holt den aktuellen (oder resetteten) Wert aus dem Modell
                    int currentValue = _diceCup.Dice[i].Value; 
                    
                    // UI aktualisieren (Rahmen/Highlight ausblenden)
                    _dieViews[i].UpdateView(currentValue, false);
                    
                    // Animator zwingen, den "Gehalten"-Zustand lautlos abzubrechen
                    _dieViews[i].ResetToIdleSilent();
                }
            }
            // ------------------------------------------------------------------

            _scoreCardView.ClearAllPotentials(); 
            UpdateMultiplayerScoreTracker();
            RefreshUIForCurrentPlayer(); // Wichtig, damit das UI den Namen anzeigt

            // Zuerst machen wir den Button für ALLE aus (Sicherheit)
            _rollButton.interactable = false;

            // Wir informieren alle (den Bot), dass ein neuer Zug beginnt
            OnTurnStarted?.Invoke();

            // Wenn es KEIN Bot ist, schalten wir den Button wieder ein
            if (CurrentPlayer.Name != "Bot")
            {
                _rollButton.interactable = true;
                _skipBotButton.gameObject.SetActive(false); //Skip Button verstecken, wenn kein Bot am Zug ist
            }
            else
            {
                _skipBotButton.gameObject.SetActive(true); //Skip Button anzeigen, wenn ein Bot am Zug ist
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
        public void OpenSettings()
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
                
                // Optional: Einen Klick-Sound abspielen
                if (DiceGame.Audio.AudioManager.Instance != null)
                {
                    // AudioManager.Instance.PlaySFX(_scoreCategorySound); // oder einen eigenen Klick-Sound
                }
            }
        }

        // Wird vom "Weiterspielen"-Button aufgerufen
        public void CloseSettings()
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }
        }

        // Wird vom "Hauptmenü"-Button aufgerufen
        public void GoToMainMenu()
        {
            // Lade die Main Menu Szene. 
            // ACHTUNG: Der Name hier muss EXAKT so lauten wie deine Szene im Projekt!
            SceneManager.LoadScene("MainMenuScene"); 
        }

        // Diese Methode schaltet die Interaktion der Buttons an oder aus
        public void SetUIInteractable(bool isInteractable)
        {
            // 1. Roll-Button sperren
            if (_rollButton != null)
            {
                _rollButton.interactable = isInteractable;
            }

            // 2. Komplettes Scoreboard sperren (Klicks gehen nicht mehr durch)
            if (_scoreCardCanvasGroup != null)
            {
                _scoreCardCanvasGroup.interactable = isInteractable;
                _scoreCardCanvasGroup.blocksRaycasts = isInteractable;
            }
        }

        public void HandleBonusClaimed()
        {
            // 1. Im Datenmodell den Bonus auf 'true' setzen
            CurrentPlayer.ScoreCard.ClaimBonus();

            // 2. Die UI aktualisieren, damit der Button aufhört zu hüpfen 
            // und das neue Total (mit den +35 Punkten) angezeigt wird.
            _scoreCardView.RefreshDisplay(CurrentPlayer.ScoreCard);

             // 3. Optional: Einen Soundeffekt abspielen
             if (DiceGame.Audio.AudioManager.Instance != null)
             {
                 DiceGame.Audio.AudioManager.Instance.PlaySFX(_bonusClaimSound);
             }
            
                // 4. Optional: Ein kurzes visuelles Feedback (z.B. eine Partikel-Explosion oder ein "+35!" Text) könnte hier auch noch hinzugefügt werden.
        }

    }
}