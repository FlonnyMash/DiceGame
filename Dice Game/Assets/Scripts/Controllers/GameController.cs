using System;
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
using DiceGame.UI.Effects;

namespace DiceGame.Controllers
{
    public class GameController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private PassDeviceView _passDeviceView;
        [SerializeField] private List<DieView> _dieViews;
        [SerializeField] private CanvasGroup _diceCanvasGroup;
        [SerializeField] private ScoreCardView _scoreCardView;
        [SerializeField] private CanvasGroup _scoreCardCanvasGroup;
        [SerializeField] private GameOverView _gameOverView;
        [SerializeField] private TextMeshProUGUI _currentPlayerNameText;
        [SerializeField] private TextMeshProUGUI _multiplayerScoreTrackerText;
        [SerializeField] private Button _skipBotButton;
        [SerializeField] private HintView _hintView;
        
        [Header("Main Action Button (Roll & Toggle)")]
        [SerializeField] private Button _mainActionButton;
        [SerializeField] private Image _mainActionIcon;
        [SerializeField] private Sprite _rollSprite;
        [SerializeField] private Sprite _hideScorecardSprite;
        [SerializeField] private Sprite _showScorecardSprite;

        [Header("Main UI Animation")]
        [SerializeField] private Animator _mainUIAnimator;

        [Header("Dice Cup Settings")]
        [SerializeField] private DiceCupView _diceCupView;
        [Tooltip("Ein unsichtbares UI-Panel, das den Bereich vorgibt, in dem die Würfel liegen dürfen.")]
        [SerializeField] private RectTransform _scatterArea;
        [SerializeField] private float _diceSpacing = 140f; 
        [SerializeField] private float _collectionRadius = 200f;
        
        [Header("VFX & Feedback (JUICE)")]
        [SerializeField] private ParticleSystem _confettiParticles;
        [SerializeField] private CameraShake _cameraShake;

        [Header("UI Panels")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Animator _settingsAnimator;

        [Header("Audio Clips")]
        [SerializeField] private AudioClip[] _rollDiceSounds;
        [SerializeField] private AudioClip _scoreCategorySound;
        [SerializeField] private AudioClip _bonusClaimSound;

        private MatchManager _matchManager;
        private LocalPlayerInput _localInput;
        private Dictionary<Player, IPlayerInput> _playerInputs;
        private Player _previousPlayer;
        private bool _isTransitioningTurn = false;
        private bool _isDiceRolling = false;

        private void Awake()
        {
            if (_mainUIAnimator != null) 
            {
                _mainUIAnimator.SetBool("IsVisible", true);
                _mainUIAnimator.Update(0f);
            }
        }

        private void Start()
        {
            _settingsPanel.SetActive(false);
            if (_passDeviceView != null) _passDeviceView.Hide();
            if (_gameOverView != null) _gameOverView.Hide();

            SetupCoreGame();
            BindUIEvents();
            BindManagerEvents();

            _matchManager.StartGame();
            LocalizationService.Instance.OnLanguageChanged += HandleLanguageChanged;
            StartCoroutine(InitializeUIRoutine());
        }

        private IEnumerator InitializeUIRoutine()
        {
            yield return new WaitForEndOfFrame();
            if (_mainUIAnimator != null) 
            {
                _mainUIAnimator.SetBool("IsVisible", true);
            }
            UpdateMainActionUI();
        }

        private void SetupCoreGame()
        {
            List<Player> players = new List<Player>();
            foreach (var name in MatchData.PlayerNames)
            {
                bool isBot = name.Contains("Bot");
                players.Add(new Player(name, isBot));
            }

            _matchManager = new MatchManager(players);
            _playerInputs = new Dictionary<Player, IPlayerInput>();
            _localInput = gameObject.AddComponent<LocalPlayerInput>();

            foreach (var p in players)
            {
                if (p.IsBot) _playerInputs.Add(p, gameObject.AddComponent<BotPlayerInput>());
                else _playerInputs.Add(p, _localInput);
            }
        }

        private void BindUIEvents()
        {
            if (_mainActionButton != null)
            {
                _mainActionButton.onClick.AddListener(HandleMainAction);
            }
            
            _scoreCardView.Initialize();
            _scoreCardView.OnCategoryClicked += (cat) => _localInput.TriggerCategorySelected(cat);
            _scoreCardView.OnBonusClaimClicked += () => _localInput.TriggerBonusClaimed();

            for (int i = 0; i < _dieViews.Count; i++)
            {
                int index = i;
                _dieViews[i].Initialize(index);
                _dieViews[i].OnDieClicked += (idx) => _localInput.TriggerToggleHold(idx);
            }

            if (_skipBotButton != null) _skipBotButton.onClick.AddListener(HandleSkipBotClicked);
            if (_passDeviceView != null) _passDeviceView.OnReadyClicked += HandlePlayerReady;

            if (_gameOverView != null)
            {
                _gameOverView.OnRestartClicked += HandleRestart;
                _gameOverView.OnMainMenuClicked += HandleMainMenu;
            }
        }

        private void HandleMainAction()
        {
            bool isScoreboardVisible = _mainUIAnimator != null && _mainUIAnimator.GetBool("IsVisible");

            if (isScoreboardVisible && _matchManager.Cup.RollsLeft > 0)
            {
                _localInput.TriggerRoll();
            }
            else
            {
                if (_mainUIAnimator != null)
                {
                    _mainUIAnimator.SetBool("IsVisible", !isScoreboardVisible);
                }
            }

            UpdateMainActionUI();
        }

        private void UpdateMainActionUI()
        {
            if (_mainActionButton == null || _mainActionIcon == null || _matchManager == null) return;

            bool isHuman = !_matchManager.CurrentPlayer.IsBot;
            bool isScoreboardVisible = _mainUIAnimator != null && _mainUIAnimator.GetBool("IsVisible");
            int rollsLeft = _matchManager.Cup.RollsLeft;

            _mainActionButton.interactable = isHuman && !_isDiceRolling;

            if (!isScoreboardVisible)
            {
                _mainActionIcon.sprite = _showScorecardSprite;
            }
            else if (rollsLeft > 0)
            {
                _mainActionIcon.sprite = _rollSprite;
            }
            else
            {
                _mainActionIcon.sprite = _hideScorecardSprite;
            }
        }

        private void BindManagerEvents()
        {
            _matchManager.OnTurnStarted += HandleTurnStarted;
            _matchManager.OnDiceRolled += HandleDiceRolled;
            _matchManager.OnDieStateChanged += HandleDieStateChanged;
            _matchManager.OnScoreApplied += HandleScoreApplied;
            _matchManager.OnBonusClaimed += HandleBonusClaimed;
            _matchManager.OnGameOver += HandleGameOver;
            _matchManager.OnTurnEnded += HandleTurnEnded;
        }

        private void HandleTurnStarted(Player player)
        {
            foreach (var input in _playerInputs.Values) input.SetActive(false);
            
            var activeInput = _playerInputs[player];
            activeInput.SetActive(true);
            _matchManager.AttachInput(activeInput);

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

        private void HandleTurnEnded(Player playerWhoJustFinished)
        {
            if (_isTransitioningTurn) return;
            _isTransitioningTurn = true;
            StartCoroutine(TurnTransitionRoutine(playerWhoJustFinished));
        }

        private IEnumerator TurnTransitionRoutine(Player playerWhoJustFinished)
        {
            float delay = playerWhoJustFinished.IsBot ? 2.5f : 1.5f;
            yield return new WaitForSeconds(delay);
            
            ResetAllDiceVisuals();
            yield return new WaitForSeconds(0.3f); 

            if (_matchManager != null) _matchManager.AdvanceToNextTurn();
            _isTransitioningTurn = false;
        }

        private void StartVisualTurn(Player player)
        {
            if (_mainUIAnimator != null) 
            {
                _mainUIAnimator.SetBool("IsVisible", true);
            }

            for (int i = 0; i < _dieViews.Count; i++)
            {
                int currentValue = _matchManager.Cup.Dice[i].Value;
                _dieViews[i].UpdateView(currentValue, false);
                _dieViews[i].ResetToIdleSilent(); 
            }

            _scoreCardView.ClearAllPotentials();
            if (_hintView != null) _hintView.HideHint();
            UpdateMultiplayerScoreTracker();
            RefreshUIForCurrentPlayer(player);

            if (player.IsBot)
            {
                SetUIInteractable(false);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(true);
                var botInput = _playerInputs[player] as BotPlayerInput;
                botInput?.StartBotTurn(_matchManager.Cup, player.ScoreCard);
            }
            else
            {
                SetUIInteractable(true);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(false);
            }

            UpdateMainActionUI();
        }

        private void HandleDiceRolled(DiceCup cup)
        {
            StartCoroutine(RollAnimationRoutine(cup));
        }

        private Vector3 GetValidScatterPosition(List<Vector3> usedPositions)
        {
            if (_scatterArea == null) return Vector3.zero;
            
            Vector3 finalWorldPos = Vector3.zero;
            bool foundValidSpot = false;
            
            // Wir nehmen die echten lokalen Grenzen des RectTransforms. 
            // xMin/xMax und yMin/yMax berücksichtigen automatisch die Größe und den Pivot des Panels!
            float padding = 60f; // Etwas erhöhtes Padding, damit die Würfel sicher nicht über den Rand lappen
            float minX = _scatterArea.rect.xMin + padding;
            float maxX = _scatterArea.rect.xMax - padding;
            float minY = _scatterArea.rect.yMin + padding;
            float maxY = _scatterArea.rect.yMax - padding;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                // 1. Zufälligen Punkt im lokalen Raum (Canvas Units) des Panels generieren
                float randX = UnityEngine.Random.Range(minX, maxX);
                float randY = UnityEngine.Random.Range(minY, maxY);
                Vector3 randomLocalPos = new Vector3(randX, randY, 0f);

                foundValidSpot = true;
                
                // 2. Distanzprüfung ebenfalls im lokalen Raum durchführen 
                // (Viel sicherer, falls der Canvas hoch- oder runterskaliert wird)
                foreach (var usedWorldPos in usedPositions)
                {
                    Vector3 usedLocalPos = _scatterArea.InverseTransformPoint(usedWorldPos);
                    if (Vector2.Distance(randomLocalPos, usedLocalPos) < _diceSpacing)
                    {
                        foundValidSpot = false;
                        break;
                    }
                }

                if (foundValidSpot)
                {
                    // 3. Den gültigen lokalen Punkt sauber in eine echte Weltkoordinate umwandeln
                    finalWorldPos = _scatterArea.TransformPoint(randomLocalPos);
                    break;
                }
            }

            return finalWorldPos;
        }

        private IEnumerator RollAnimationRoutine(DiceCup cup)
        {
            _isDiceRolling = true;
            UpdateMainActionUI(); 

            _scoreCardView.ClearAllPotentials();
            _scoreCardView.ClearAllHighlights();
            if (_hintView != null) _hintView.HideHint();
            if (_diceCanvasGroup != null) _diceCanvasGroup.blocksRaycasts = false;

            if (_mainUIAnimator != null) 
            {
                _mainUIAnimator.SetBool("IsVisible", false);
            }

            bool allDiceHeld = cup.Dice.All(die => die.IsHeld);
            bool isHuman = !_matchManager.CurrentPlayer.IsBot;

            if (!allDiceHeld)
            {
                // NEU: Wir warten hier, bis die UI Slide-Out Animation abgeschlossen ist,
                // bevor wir die Würfel um 200 auf der Y-Achse verschieben.
                // 0.4 Sekunden ist ein solider Durchschnittswert für Animator-Transitions.
                yield return new WaitForSeconds(1.0f);

                List<Coroutine> slideRoutines = new List<Coroutine>();
                List<DieView> diceToCollect = new List<DieView>();
                
                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld)
                    {
                        Vector2 stagingPos = _dieViews[i].InitialPosition + new Vector2(0, 200f);
                        slideRoutines.Add(_dieViews[i].SlideToPosition(stagingPos, 0.3f));
                        diceToCollect.Add(_dieViews[i]);
                    }
                }
                
                foreach (var routine in slideRoutines) yield return routine;
                yield return new WaitForSeconds(0.2f);

                if (_diceCupView != null)
                {
                    bool allCollected = false;
                    bool shakeFinished = false;

                    Action onDrag = () => 
                    {
                        for (int i = diceToCollect.Count - 1; i >= 0; i--)
                        {
                            if (Vector3.Distance(_diceCupView.CupImageRect.position, diceToCollect[i].Rect.position) < _collectionRadius)
                            {
                                diceToCollect[i].SetVisibility(false);
                                diceToCollect.RemoveAt(i);
                            }
                        }
                        if (diceToCollect.Count == 0) allCollected = true;
                    };

                    Action onShakeComplete = () => 
                    {
                        if (allCollected) shakeFinished = true;
                    };
                    
                    _diceCupView.OnCupDragged += onDrag;
                    _diceCupView.OnShakeCompleted += onShakeComplete;
                    
                    if (isHuman)
                    {
                        _diceCupView.EnableInteraction();
                        while (!shakeFinished) yield return null;
                    }
                    else
                    {
                        foreach (var d in diceToCollect) d.SetVisibility(false);
                        yield return _diceCupView.AutoShakeRoutine();
                    }
                    
                    _diceCupView.OnCupDragged -= onDrag;
                    _diceCupView.OnShakeCompleted -= onShakeComplete;
                    
                    _diceCupView.DisableInteraction();
                }
            }

            List<Vector3> usedPositions = new List<Vector3>();
            
            for (int i = 0; i < cup.Dice.Count; i++)
            {
                if (!cup.Dice[i].IsHeld)
                {
                    Vector3 finalPos = GetValidScatterPosition(usedPositions);
                    usedPositions.Add(finalPos);
                    
                    float randomRot = UnityEngine.Random.Range(0f, 360f);
                    
                    _dieViews[i].UpdateView(cup.Dice[i].Value, false);
                    _dieViews[i].ScatterWorld(finalPos, randomRot);
                }
            }

            if (!allDiceHeld)
            {
                if (_rollDiceSounds != null && _rollDiceSounds.Length > 0 && isHuman)
                {
                    AudioManager.Instance.PlaySFX(_rollDiceSounds[UnityEngine.Random.Range(0, _rollDiceSounds.Length)]);
                }
                
                if (_diceCupView != null)
                {
                    yield return _diceCupView.AnimateRevealRoutine();
                }

                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld) _dieViews[i].SetVisibility(true);
                }

                if (_diceCanvasGroup != null && isHuman) _diceCanvasGroup.blocksRaycasts = true;
                yield return new WaitForSeconds(2.0f); 

                if (_diceCanvasGroup != null && isHuman) _diceCanvasGroup.blocksRaycasts = false;

                List<Coroutine> returnRoutines = new List<Coroutine>();
                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld)
                    {
                        returnRoutines.Add(_dieViews[i].SlideBackToTray(0.6f));
                    }
                }
                
                yield return new WaitForSeconds(0.65f); 
            }

            if (_mainUIAnimator != null) _mainUIAnimator.SetBool("IsVisible", true);
            if (_diceCupView != null) _diceCupView.ResetCup();

            _isDiceRolling = false;
            UpdatePotentialScores(cup, _matchManager.CurrentPlayer);

            if (isHuman && _hintView != null)
            {
                ScoreCategory? bestHint = HintCalculator.GetBestHint(_matchManager.CurrentPlayer.ScoreCard, cup.Dice, cup.RollsLeft);
                if (bestHint.HasValue) _hintView.ShowHint(bestHint.Value);
            }

            if (_diceCanvasGroup != null && isHuman) _diceCanvasGroup.blocksRaycasts = true;
            
            UpdateMainActionUI();
        }

        private void HandleDieStateChanged(int index, bool isHeld)
        {
            int currentValue = _matchManager.Cup.Dice[index].Value;
            _dieViews[index].UpdateView(currentValue, isHeld);
            _dieViews[index].PlayToggleAnimation(isHeld);
            
            bool isScoreboardVisible = _mainUIAnimator != null && _mainUIAnimator.GetBool("IsVisible");

            if (!isScoreboardVisible)
            {
                if (!isHeld)
                {
                    List<Vector3> usedPositions = new List<Vector3>();
                    for (int i = 0; i < _dieViews.Count; i++)
                    {
                        if (i != index && !_matchManager.Cup.Dice[i].IsHeld)
                        {
                            usedPositions.Add(_dieViews[i].ScatteredWorldPosition);
                        }
                    }
                    
                    Vector3 newPos = GetValidScatterPosition(usedPositions);
                    float randomRot = UnityEngine.Random.Range(0f, 360f);
                    _dieViews[index].SetScatterTargetWorld(newPos, randomRot);
                }
                _dieViews[index].AnimateToState(isHeld);
            }
        }

        private void HandleScoreApplied(Player player, ScoreCategory category, int points)
        {
            StartCoroutine(WaitAndApplyScore(player, category, points));
        }

        private IEnumerator WaitAndApplyScore(Player player, ScoreCategory category, int points)
        {
            while (_isDiceRolling) yield return null;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(_scoreCategorySound);

            _scoreCardView.SetFinalScore(category, points);
            _scoreCardView.ClearAllPotentials();
            _scoreCardView.UpdateTotals(player.ScoreCard.UpperSectionRaw, player.ScoreCard.UpperSectionBonus, player.ScoreCard.GrandTotal, player.ScoreCard.IsBonusClaimed);
            UpdateMultiplayerScoreTracker();

            if (category == ScoreCategory.NicerDicer && points > 0)
            {
                if (_confettiParticles != null) _confettiParticles.Play();
                if (_cameraShake != null) _cameraShake.Shake(0.5f, 0.2f);
            }
            
            UpdateMainActionUI();
        }

        private void ResetAllDiceVisuals()
        {
            if (_dieViews == null) return;
            for (int i = 0; i < _dieViews.Count; i++)
            {
                _dieViews[i].AnimateReset();
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
            
            if (rankings.Count == 1) _gameOverView.ShowSinglePlayer(rankings[0].ScoreCard.GrandTotal);
            else _gameOverView.ShowMultiPlayer(rankings);
        }

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
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("new_record", currentScore);
                        _currentPlayerNameText.color = Color.green;
                    }
                    else
                    {
                        int displayScore = Mathf.Max(currentHighScore, currentScore);
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("high_score", displayScore);
                        _currentPlayerNameText.color = Color.yellow;
                    }
                }
                else
                {
                    _currentPlayerNameText.text = LocalizationService.Instance.GetText("turn_indicator", player.Name);
                    _currentPlayerNameText.color = player.IsBot ? Color.red : Color.white;
                }
            }
        }

        private void UpdateMultiplayerScoreTracker()
        {
            if (_multiplayerScoreTrackerText == null || _matchManager.Players.Count <= 1) return;

            _multiplayerScoreTrackerText.gameObject.SetActive(true);
            string trackerString = string.Join("   |   ", _matchManager.Players.Select(p => $"{p.Name}: {p.ScoreCard.GrandTotal}"));
            _multiplayerScoreTrackerText.text = trackerString;
        }

        public void SetUIInteractable(bool isInteractable)
        {
            if (_mainActionButton != null) _mainActionButton.interactable = isInteractable;
            if (_scoreCardCanvasGroup != null)
            {
                _scoreCardCanvasGroup.interactable = isInteractable;
                _scoreCardCanvasGroup.blocksRaycasts = isInteractable;
            }
        }

        public void OpenSettings() 
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
                if (_settingsAnimator != null) _settingsAnimator.SetBool("IsVisible", true);
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
                yield return new WaitForSeconds(0.5f);
            }
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        public void GoToMainMenu() { SceneManager.LoadScene("MainMenuScene"); }
        private void HandleRestart() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }
        private void HandleMainMenu() { SceneManager.LoadScene("MainMenuScene"); }

        private void OnDestroy()
        {
            if (_mainActionButton != null) _mainActionButton.onClick.RemoveListener(HandleMainAction);
            LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            if (_matchManager != null) _matchManager.OnTurnEnded -= HandleTurnEnded;
        }

        private void HandleLanguageChanged()
        {
            RefreshUIForCurrentPlayer(_matchManager.CurrentPlayer);
            _scoreCardView.UpdateTranslations();
            _scoreCardView.RefreshDisplay(_matchManager.CurrentPlayer.ScoreCard);
        }
    }
}