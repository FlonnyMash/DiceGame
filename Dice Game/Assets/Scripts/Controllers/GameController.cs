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
using DiceGame.Audio;
using DiceGame.Core.Systems;
using DiceGame.Core.Inputs;
using DiceGame.Core.Interfaces;
using DiceGame.Services;

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
        [SerializeField] private TextMeshProUGUI _currentPlayerNameText;
        [SerializeField] private TextMeshProUGUI _multiplayerScoreTrackerText;
        [SerializeField] private Button _skipBotButton;
        
        // _botController wurde entfernt, wir nutzen jetzt das Input-System

        [Header("UI Panels")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Animator _settingsAnimator;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip[] _rollDiceSounds;
        [SerializeField] private AudioClip _scoreCategorySound;
        [SerializeField] private AudioClip _bonusClaimSound;

        // --- CORE SYSTEM REFERENZEN ---
        private MatchManager _matchManager;
        private LocalPlayerInput _localInput;
        private Dictionary<Player, IPlayerInput> _playerInputs;
        private Player _previousPlayer;
        private bool _isTransitioningTurn = false;
        private bool _isDiceRolling = false;

        private void Start()
        {
            _settingsPanel.SetActive(false);
            if (_passDeviceView != null) _passDeviceView.Hide();
            if (_gameOverView != null) _gameOverView.Hide();

            SetupCoreGame();
            BindUIEvents();
            BindManagerEvents();

            // Spiel starten!
            _matchManager.StartGame();

            // Abonniere Sprachwechsel, um UI sofort zu aktualisieren
            LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
        }

        private void SetupCoreGame()
        {
            // 1. Spieler-Modelle aus dem Main Menu laden
            List<Player> players = new List<Player>();
            foreach (var name in MatchData.PlayerNames)
            {
                // Erkennen, ob es sich um den Bot handelt
                bool isBot = name.Contains("Bot");
                players.Add(new Player(name, isBot));
            }

            // 2. Den MatchManager instanziieren (Reine C#-Logik!)
            _matchManager = new MatchManager(players);

            // 3. Inputs dynamisch erstellen und zuweisen
            _playerInputs = new Dictionary<Player, IPlayerInput>();
            _localInput = gameObject.AddComponent<LocalPlayerInput>(); // Ein Input für alle Menschen

            foreach (var p in players)
            {
                if (p.IsBot)
                {
                    _playerInputs.Add(p, gameObject.AddComponent<BotPlayerInput>());
                }
                else
                {
                    _playerInputs.Add(p, _localInput);
                }
            }
        }

        private void BindUIEvents()
        {
            // UI -> Local Input (Wir leiten Klicks nur noch an den Input weiter!)
            _rollButton.onClick.AddListener(() => _localInput.TriggerRoll());
            
            _scoreCardView.Initialize();
            _scoreCardView.OnCategoryClicked += (cat) => _localInput.TriggerCategorySelected(cat);
            _scoreCardView.OnBonusClaimClicked += () => _localInput.TriggerBonusClaimed();

            for (int i = 0; i < _dieViews.Count; i++)
            {
                int index = i;
                _dieViews[i].Initialize(index);
                _dieViews[i].OnDieClicked += (idx) => _localInput.TriggerToggleHold(idx);
            }

            if (_skipBotButton != null)
            {
                _skipBotButton.onClick.AddListener(HandleSkipBotClicked);
            }

            if (_passDeviceView != null)
            {
                _passDeviceView.OnReadyClicked += HandlePlayerReady;
            }

            if (_gameOverView != null)
            {
                _gameOverView.OnRestartClicked += HandleRestart;
                _gameOverView.OnMainMenuClicked += HandleMainMenu;
            }
        }

        private void BindManagerEvents()
        {
            // MatchManager -> UI (Wir hören auf die Logik und spielen Animationen ab)
            _matchManager.OnTurnStarted += HandleTurnStarted;
            _matchManager.OnDiceRolled += HandleDiceRolled;
            _matchManager.OnDieStateChanged += HandleDieStateChanged;
            _matchManager.OnScoreApplied += HandleScoreApplied;
            _matchManager.OnBonusClaimed += HandleBonusClaimed;
            _matchManager.OnGameOver += HandleGameOver;
            _matchManager.OnTurnEnded += HandleTurnEnded;
        }

        // ==========================================
        // EVENT HANDLER (Von der Kernlogik zur UI)
        // ==========================================

        private void HandleTurnStarted(Player player)
        {
            // 1. Welcher Input ist dran?
            foreach (var input in _playerInputs.Values) input.SetActive(false);
            
            var activeInput = _playerInputs[player];
            activeInput.SetActive(true);
            _matchManager.AttachInput(activeInput);

            // 2. Pass Device Logik
            bool showPassDevice = false;
            if (_matchManager.Players.Count > 1 && _previousPlayer != null)
            {
                bool wasHuman = !_previousPlayer.IsBot;
                bool isNextHuman = !player.IsBot;
                if (wasHuman && isNextHuman) showPassDevice = true;
            }
            

            if (showPassDevice)
            {
                SetUIInteractable(false);
                _passDeviceView.Show(player.Name);
            }
            else
            {
                StartVisualTurn(player);
            }

            _previousPlayer = player;
        }
        
        // 1. Der Parameter (Player playerWhoJustFinished) MUSS in die Klammern!
        private void HandleTurnEnded(Player playerWhoJustFinished)
        {
            // Wir nutzen exakt den Variablennamen aus Zeile 49!
            if (_isTransitioningTurn) return; 
            
            _isTransitioningTurn = true;
            
            // 2. Wir geben das Paket an die Coroutine weiter
            StartCoroutine(TurnTransitionRoutine(playerWhoJustFinished));
        }

        // 3. Auch hier MUSS der Parameter in die Klammern!
        private System.Collections.IEnumerator TurnTransitionRoutine(Player playerWhoJustFinished)
        {
            float delay = playerWhoJustFinished.IsBot ? 2.5f : 1.5f;
            yield return new WaitForSeconds(delay);

            ResetAllDiceVisuals();

            if (_matchManager != null)
            {
                _matchManager.AdvanceToNextTurn();
            }

            // 4. Türsteher wieder ausschalten (exakter Name aus Zeile 49!)
            _isTransitioningTurn = false; 
        }

        private void StartVisualTurn(Player player)
        {
            // UI aufräumen
            for (int i = 0; i < _dieViews.Count; i++)
            {
                int currentValue = _matchManager.Cup.Dice[i].Value;
                _dieViews[i].UpdateView(currentValue, false);
                _dieViews[i].ResetToIdleSilent();
            }

            _scoreCardView.ClearAllPotentials();
            UpdateMultiplayerScoreTracker();
            RefreshUIForCurrentPlayer(player);

            // Ist es ein Bot?
            if (player.IsBot)
            {
                SetUIInteractable(false);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(true);
                
                // Dem Bot Bescheid geben, dass er starten darf
                var botInput = _playerInputs[player] as BotPlayerInput;
                botInput?.StartBotTurn(_matchManager.Cup, player.ScoreCard);
            }
            else
            {
                SetUIInteractable(true);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(false);
            }
        }

        private void HandleDiceRolled(DiceCup cup)
        {
            StartCoroutine(RollAnimationRoutine(cup));
        }

        private IEnumerator RollAnimationRoutine(DiceCup cup)
        {
            _isDiceRolling = true;
            
            if (_diceCanvasGroup != null) _diceCanvasGroup.blocksRaycasts = false;
            _rollButton.interactable = false;

            bool allDiceHeld = cup.Dice.All(die => die.IsHeld);
            bool isHuman = !_matchManager.CurrentPlayer.IsBot;

            if (!allDiceHeld && isHuman && _rollDiceSounds != null && _rollDiceSounds.Length > 0)
            {
                int randomIndex = UnityEngine.Random.Range(0, _rollDiceSounds.Length);
                AudioManager.Instance.PlaySFX(_rollDiceSounds[randomIndex]);
            }

            float duration = 1.5f;
            for (int i = 0; i < cup.Dice.Count; i++)
            {
                _dieViews[i].AnimateRoll(cup.Dice[i].Value, duration);
            }

            yield return new WaitForSeconds(duration);

            if (cup.RollsLeft > 0 && !_matchManager.CurrentPlayer.IsBot)
            {
                _rollButton.interactable = true;
            }

            if (_diceCanvasGroup != null && !_matchManager.CurrentPlayer.IsBot)
            {
                _diceCanvasGroup.blocksRaycasts = true;
            }

            _isDiceRolling = false;
            UpdatePotentialScores(cup, _matchManager.CurrentPlayer);
        }

        private void HandleDieStateChanged(int index, bool isHeld)
        {
            int currentValue = _matchManager.Cup.Dice[index].Value;
            _dieViews[index].UpdateView(currentValue, isHeld);
            _dieViews[index].PlayToggleAnimation(isHeld);
        }

        private void HandleScoreApplied(Player player, ScoreCategory category, int points)
        {
            // Wir starten eine kleine Hilfs-Routine, die wartet, bis die Würfel liegen
            StartCoroutine(WaitAndApplyScore(player, category, points));
        }

        private IEnumerator WaitAndApplyScore(Player player, ScoreCategory category, int points)
        {
            // Warte so lange, wie die Würfel noch in Bewegung sind
            while (_isDiceRolling)
            {
                yield return null; 
            }

            // Erst JETZT werden die Punkte visuell eingetragen
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(_scoreCategorySound);

            _scoreCardView.SetFinalScore(category, points);
            _scoreCardView.ClearAllPotentials();
            _scoreCardView.UpdateTotals(
                player.ScoreCard.UpperSectionRaw,
                player.ScoreCard.UpperSectionBonus,
                player.ScoreCard.GrandTotal
            );

            UpdateMultiplayerScoreTracker();
        }


        private void ResetAllDiceVisuals()
        {
            if (_dieViews == null) return;

            // Geht alle deine Würfel durch und setzt sie lautlos zurück
            for (int i = 0; i < _dieViews.Count; i++)
            {
                _dieViews[i].ResetToIdleSilent();
            }
        }

        private void HandleBonusClaimed(Player player)
        {
            _scoreCardView.RefreshDisplay(player.ScoreCard);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(_bonusClaimSound);
        }

        private void HandleGameOver(List<Player> rankings)
        {
            SetUIInteractable(false);
            if (_gameOverView == null) return;

            if (rankings.Count == 1)
            {
                _gameOverView.ShowSinglePlayer(rankings[0].ScoreCard.GrandTotal);
            }
            else
            {
                _gameOverView.ShowMultiPlayer(rankings);
            }
        }

        // ==========================================
        // UI HELPER METHODEN
        // ==========================================

        private void HandleSkipBotClicked()
        {
            if (_matchManager.CurrentPlayer.IsBot)
            {
                var botInput = _playerInputs[_matchManager.CurrentPlayer] as BotPlayerInput;
                botInput?.SkipBotTurn(_matchManager.Cup, _matchManager.CurrentPlayer.ScoreCard);
            }
        }

        private void HandlePlayerReady()
        {
            if (_passDeviceView != null) _passDeviceView.Hide();
            StartVisualTurn(_matchManager.CurrentPlayer);
        }

        private void UpdatePotentialScores(DiceCup cup, Player player)
        {
            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                if (!player.ScoreCard.IsCategoryFilled(category))
                {
                    int potentialScore = ScoreCalculator.CalculateScore(cup.Dice, category);
                    _scoreCardView.ShowPotentialScore(category, potentialScore);
                }
            }
        }

        private void RefreshUIForCurrentPlayer(Player player)
        {
            _scoreCardView.RefreshDisplay(player.ScoreCard);

            if (_currentPlayerNameText != null)
            {
                if (_matchManager.Players.Count == 1)
                {
                    int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
                    int currentScore = player.ScoreCard.GrandTotal;

                    if (currentScore > currentHighScore && currentHighScore > 0)
                    {
                        // NEU: Nutze den Service für den Rekord-Text
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("new_record", currentScore);
                        _currentPlayerNameText.color = Color.green;
                    }
                    else
                    {
                        int displayScore = Mathf.Max(currentHighScore, currentScore);
                        // NEU: Nutze den Service für den Highscore-Text
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("high_score", displayScore);
                        _currentPlayerNameText.color = Color.yellow;
                    }
                }
                else
                {
                    // NEU: Nutze den Service für die Anzeige, wer am Zug ist
                    _currentPlayerNameText.text = LocalizationService.Instance.GetText("turn_indicator", player.Name);
                    _currentPlayerNameText.color = player.IsBot ? Color.red : Color.white;
                }
            }
        }

        private void UpdateMultiplayerScoreTracker()
        {
            if (_multiplayerScoreTrackerText == null || _matchManager.Players.Count <= 1)
            {
                if (_multiplayerScoreTrackerText != null) _multiplayerScoreTrackerText.gameObject.SetActive(false);
                return;
            }

            _multiplayerScoreTrackerText.gameObject.SetActive(true);
            string trackerString = string.Join("   |   ", _matchManager.Players.Select(p => $"{p.Name}: {p.ScoreCard.GrandTotal}"));
            _multiplayerScoreTrackerText.text = trackerString;
        }

        public void SetUIInteractable(bool isInteractable)
        {
            if (_rollButton != null) _rollButton.interactable = isInteractable;
            if (_scoreCardCanvasGroup != null)
            {
                _scoreCardCanvasGroup.interactable = isInteractable;
                _scoreCardCanvasGroup.blocksRaycasts = isInteractable;
            }
        }

        // ==========================================
        // SCENE MANAGEMENT & SETTINGS
        // ==========================================

        public void OpenSettings() 
        { 
            if (_settingsPanel != null) 
            {
                _settingsPanel.SetActive(true); 
                if (_settingsAnimator != null)
                {
                    _settingsAnimator.SetBool("IsVisible", true); 
                }
            }
        }

        public void CloseSettings() 
        { 
            StartCoroutine(CloseSettingsRoutine()); 
        }

        private IEnumerator CloseSettingsRoutine()
        {
            if (_settingsAnimator != null)
            {
                _settingsAnimator.SetBool("IsVisible", false);
                // Warte, bis die Slide-Out Animation fertig ist (Zeit ggf. anpassen)
                yield return new WaitForSeconds(0.5f); 
            }
            
            if (_settingsPanel != null) 
            {
                _settingsPanel.SetActive(false);
            }
        }

        public void GoToMainMenu() 
        { 
            SceneManager.LoadScene("MainMenuScene"); 
        }

        private void HandleRestart() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        private void HandleMainMenu() { SceneManager.LoadScene("MainMenuScene"); }

        private void OnDestroy()
        {
            if (_rollButton != null) _rollButton.onClick.RemoveAllListeners();
            LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            if (_matchManager != null)
            {
                _matchManager.OnTurnEnded -= HandleTurnEnded;
            }
        }

        private void HandleLanguageChanged()
        {
            // Aktualisiert Namen und Turn-Anzeige
            RefreshUIForCurrentPlayer(_matchManager.CurrentPlayer);
            
            // NEU: Aktualisiert die Punktekarte (Einser, Full House etc.)
            _scoreCardView.UpdateTranslations();
            _scoreCardView.RefreshDisplay(_matchManager.CurrentPlayer.ScoreCard);
        }
    }
    
}