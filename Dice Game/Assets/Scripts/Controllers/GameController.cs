using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
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
using DiceGame.Configs; 

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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        [Header("Debug & Cheats")]
        [SerializeField] private DebugMenuView _debugMenuView;
#endif

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
        private bool _isScoreboardVisibleInternal = true;
        private bool _canStartRoll = true;
        
        // #region agent log
        private static void AgentLog(string runId, string hypothesisId, string location, string message, string dataJson)
        {
            try
            {
                File.AppendAllText("debug-f7e117.log", $"{{\"sessionId\":\"f7e117\",\"runId\":\"{runId}\",\"hypothesisId\":\"{hypothesisId}\",\"location\":\"{location}\",\"message\":\"{message}\",\"data\":{dataJson},\"timestamp\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n");
            }
            catch { }
        }
        // #endregion

        private void Awake()
        {
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

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (_debugMenuView != null)
            {
                _debugMenuView.OnForceSixesClicked += () => ApplyCheat(new int[] { 6, 6, 6, 6, 6 });
                _debugMenuView.OnForceStraightClicked += () => ApplyCheat(new int[] { 1, 2, 3, 4, 5 });
                _debugMenuView.OnResetBoardClicked += ResetMatchHard;
                _debugMenuView.OnForceBonusClicked += ForceUpperBonus;
            }
#endif
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // --- DEBUG & CHEAT METHODEN ---
        private void ApplyCheat(int[] forcedValues)
        {
            if (_isDiceRolling) return;
            if (_matchManager == null || _matchManager.CurrentPlayer == null) return;
            if (_matchManager.CurrentPlayer.IsBot) return;

            for (int i = 0; i < _matchManager.Cup.Dice.Count; i++)
            {
                if (i < forcedValues.Length)
                {
                    _matchManager.Cup.Dice[i].DebugForceValue(forcedValues[i]);
                    _dieViews[i].UpdateView(forcedValues[i], _matchManager.Cup.Dice[i].IsHeld);
                }
            }

            UpdatePotentialScores(_matchManager.Cup, _matchManager.CurrentPlayer);

            if (_hintView != null)
            {
                ScoreCategory? bestHint = HintCalculator.GetBestHint(
                    _matchManager.CurrentPlayer.ScoreCard,
                    _matchManager.Cup.Dice,
                    _matchManager.Cup.RollsLeft);

                if (bestHint.HasValue) _hintView.ShowHint(bestHint.Value);
            }
        }

        private void ResetMatchHard()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void ForceUpperBonus()
        {
            if (_matchManager == null || _matchManager.CurrentPlayer == null) return;
            Player player = _matchManager.CurrentPlayer;

            player.ScoreCard.SetScore(ScoreCategory.Ones, 3);
            player.ScoreCard.SetScore(ScoreCategory.Twos, 6);
            player.ScoreCard.SetScore(ScoreCategory.Threes, 9);
            player.ScoreCard.SetScore(ScoreCategory.Fours, 12);
            player.ScoreCard.SetScore(ScoreCategory.Fives, 15);
            player.ScoreCard.SetScore(ScoreCategory.Sixes, 18);

            _scoreCardView.SetFinalScore(ScoreCategory.Ones, 3);
            _scoreCardView.SetFinalScore(ScoreCategory.Twos, 6);
            _scoreCardView.SetFinalScore(ScoreCategory.Threes, 9);
            _scoreCardView.SetFinalScore(ScoreCategory.Fours, 12);
            _scoreCardView.SetFinalScore(ScoreCategory.Fives, 15);
            _scoreCardView.SetFinalScore(ScoreCategory.Sixes, 18);

            _scoreCardView.UpdateTotals(
                player.ScoreCard.UpperSectionRaw,
                player.ScoreCard.UpperSectionBonus,
                player.ScoreCard.GrandTotal,
                player.ScoreCard.IsBonusClaimed
            );
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ApplyCheat(new int[] { 6, 6, 6, 6, 6 });
            }
        }
#endif

        private IEnumerator InitializeUIRoutine()
        {
            yield return new WaitForEndOfFrame();
            SetScoreboardVisibility(true);
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

            if (_diceCupView != null)
            {
                _diceCupView.OnCupTouched += HandleCupDragToRoll;
            }

            if (_skipBotButton != null) _skipBotButton.onClick.AddListener(HandleSkipBotClicked);

            if (_passDeviceView != null) _passDeviceView.OnReadyClicked += HandlePlayerReady;
            if (_gameOverView != null)
            {
                _gameOverView.OnRestartClicked += HandleRestart;
                _gameOverView.OnMainMenuClicked += HandleMainMenu;
            }
        }

        // ==========================================
        //  UI / TOGGLE LOGIK
        // ==========================================
        private void HandleMainAction()
        {
            if (_isDiceRolling || _mainUIAnimator == null || _isTransitioningTurn) return;

            SetScoreboardVisibility(!_isScoreboardVisibleInternal);
        }

        private void SetScoreboardVisibility(bool isVisible)
        {
            _isScoreboardVisibleInternal = isVisible;

            if (isVisible)
            {
                ResetAllDiceToTray();
            }
            else
            {
                RestoreAllDiceFromTray();
            }

            if (_mainUIAnimator != null)
            {
                _mainUIAnimator.SetBool("IsVisible", _isScoreboardVisibleInternal);
            }
            UpdateMainActionUI();
        }

        private void RestoreAllDiceFromTray()
        {
            if (_dieViews == null || _matchManager?.Cup == null) return;
            for (int i = 0; i < _dieViews.Count; i++)
            {
                var dieData = _matchManager.Cup.Dice[i];
                _dieViews[i].PlayToggleAnimation(dieData.IsHeld);
            }
        }

        private void ResetAllDiceToTray()
        {
            if (_dieViews == null) return;
            foreach (var die in _dieViews)
            {
                // ResetToIdleSilent ist hier perfekt, da es ohne Verzögerung oder sichtbare Animation 
                // die Transforms bereinigt, während das Scoreboard gerade eingeblendet wird.
                die.ResetToIdleSilent();
            }
        }

        private void UpdateMainActionUI()
        {
            if (_mainActionButton == null || _mainActionIcon == null || _matchManager == null) return;

            bool isHuman = !_matchManager.CurrentPlayer.IsBot;

            _mainActionButton.interactable = isHuman && !_isDiceRolling && !_isTransitioningTurn;

            if (_isScoreboardVisibleInternal)
            {
                _mainActionIcon.sprite = _hideScorecardSprite;
            }
            else
            {
                _mainActionIcon.sprite = _showScorecardSprite;
            }
        }

        // ==========================================
        //  WÜRFEL-LOGIK (CUP & ANIMATION)
        // ==========================================
        private void HandleCupDragToRoll()
        {
            if (_isDiceRolling || !_canStartRoll || _matchManager == null || _isTransitioningTurn) return;
            if (_matchManager.Cup.RollsLeft <= 0 || _matchManager.CurrentPlayer.IsBot) return;

            _canStartRoll = false;
            _localInput.TriggerRoll();
        }

        private void HandleDiceRolled(DiceCup cup)
        {
            StartCoroutine(RollAnimationRoutine(cup));
        }

        private IEnumerator RollAnimationRoutine(DiceCup cup)
        {
            _isDiceRolling = true;
            // #region agent log
            AgentLog("pre-fix", "H1", "GameController.RollAnimationRoutine", "roll animation started", $"{{\"playerIsBot\":{_matchManager.CurrentPlayer.IsBot.ToString().ToLower()},\"rollsLeftNow\":{cup.RollsLeft}}}");
            // #endregion
            UpdateMainActionUI();

            _scoreCardView.ClearAllPotentials();
            _scoreCardView.ClearAllHighlights();
            if (_hintView != null) _hintView.HideHint();
            if (_diceCanvasGroup != null) _diceCanvasGroup.blocksRaycasts = false;

            bool isHuman = !_matchManager.CurrentPlayer.IsBot;
            if (!isHuman)
            {
                yield return PlayBotScoreboardRollRoutine(cup);
                _isDiceRolling = false;
                _canStartRoll = true;
                _matchManager.NotifyRollAnimationCompleted();

                SetScoreboardVisibility(true);
                if (_diceCupView != null) _diceCupView.ResetCup();

                UpdatePotentialScores(cup, _matchManager.CurrentPlayer);
                UpdateMainActionUI();
                yield break;
            }

            SetScoreboardVisibility(false);
            // #region agent log
            AgentLog("pre-fix-2", "H5", "GameController.RollAnimationRoutine", "scoreboard hidden before dice lift", $"{{\"playerIsBot\":{_matchManager.CurrentPlayer.IsBot.ToString().ToLower()},\"animatorVisibleFlag\":{(_mainUIAnimator != null && _mainUIAnimator.GetBool("IsVisible")).ToString().ToLower()}}}");
            // #endregion

            bool allDiceHeld = cup.Dice.All(die => die.IsHeld);

            if (!allDiceHeld)
            {
                // #region agent log
                AgentLog("pre-fix-2", "H5", "GameController.RollAnimationRoutine", "dice lift to y+200 started", $"{{\"playerIsBot\":{(!isHuman).ToString().ToLower()},\"remainingRolls\":{cup.RollsLeft}}}");
                // #endregion
                List<Coroutine> slideRoutines = new List<Coroutine>();
                List<DieView> diceToCollect = new List<DieView>();

                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld)
                    {
                        Vector2 stagingPos = _dieViews[i].InitialPosition + new Vector2(0, 200f);
                        slideRoutines.Add(_dieViews[i].SlideToPosition(stagingPos, 0.4f));
                        diceToCollect.Add(_dieViews[i]);
                    }
                }

                foreach (var routine in slideRoutines) yield return routine;
                // #region agent log
                AgentLog("pre-fix-2", "H5", "GameController.RollAnimationRoutine", "dice lift to y+200 finished", $"{{\"playerIsBot\":{(!isHuman).ToString().ToLower()},\"diceToCollectCount\":{diceToCollect.Count}}}");
                // #endregion
                yield return new WaitForSeconds(0.2f);

                if (_diceCupView != null)
                {
                    bool allCollected = false;
                    bool shakeFinished = false;

                    Action onDrag = () => {
                        for (int i = diceToCollect.Count - 1; i >= 0; i--) {
                            if (Vector3.Distance(_diceCupView.CupImageRect.position, diceToCollect[i].Rect.position) < _collectionRadius) {
                                diceToCollect[i].SetVisibility(false);
                                diceToCollect.RemoveAt(i);
                            }
                        }
                        if (diceToCollect.Count == 0) allCollected = true;
                    };

                    Action onShakeComplete = () => { if (allCollected) shakeFinished = true; };

                    _diceCupView.OnCupDragged += onDrag;
                    _diceCupView.OnShakeCompleted += onShakeComplete;

                    if (isHuman) {
                        _diceCupView.EnableInteraction();
                        while (!shakeFinished) yield return null;
                    } else {
                        // #region agent log
                        AgentLog("pre-fix-2", "H6", "GameController.RollAnimationRoutine", "bot starts collecting and shaking", $"{{\"diceToCollectBefore\":{diceToCollect.Count}}}");
                        // #endregion
                        foreach (var d in diceToCollect) d.SetVisibility(false);
                        yield return _diceCupView.AutoShakeRoutine();
                        // #region agent log
                        AgentLog("pre-fix-2", "H6", "GameController.RollAnimationRoutine", "bot finished auto shake", $"{{\"diceToCollectAfter\":{diceToCollect.Count}}}");
                        // #endregion
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

                if (_diceCupView != null) yield return _diceCupView.AnimateRevealRoutine();

                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld) _dieViews[i].SetVisibility(true);
                }

                yield return new WaitForSeconds(2.0f);

                List<Coroutine> returnRoutines = new List<Coroutine>();
                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld) returnRoutines.Add(_dieViews[i].SlideBackToTray(0.6f));
                }

                yield return new WaitForSeconds(0.65f);
            }

            _isDiceRolling = false;
            _canStartRoll = true;
            // #region agent log
            AgentLog("pre-fix", "H1", "GameController.RollAnimationRoutine", "roll animation finished", $"{{\"playerIsBot\":{_matchManager.CurrentPlayer.IsBot.ToString().ToLower()},\"rollsLeftNow\":{cup.RollsLeft}}}");
            // #endregion
            _matchManager.NotifyRollAnimationCompleted();

            SetScoreboardVisibility(true);

            if (_diceCupView != null)
            {
                _diceCupView.ResetCup();
                if (isHuman && cup.RollsLeft > 0)
                {
                    _diceCupView.EnableInteraction();
                }
            }

            UpdatePotentialScores(cup, _matchManager.CurrentPlayer);

            if (isHuman && _hintView != null)
            {
                ScoreCategory? bestHint = HintCalculator.GetBestHint(_matchManager.CurrentPlayer.ScoreCard, cup.Dice, cup.RollsLeft);
                if (bestHint.HasValue) _hintView.ShowHint(bestHint.Value);
            }

            if (_diceCanvasGroup != null && isHuman) _diceCanvasGroup.blocksRaycasts = true;

            UpdateMainActionUI();
        }

        private IEnumerator PlayBotScoreboardRollRoutine(DiceCup cup)
        {
            // #region agent log
            AgentLog("post-fix", "H8", "GameController.PlayBotScoreboardRollRoutine", "bot scoreboard roll animation started", $"{{\"rollsLeft\":{cup.RollsLeft}}}");
            // #endregion
            SetScoreboardVisibility(true);

            for (int i = 0; i < cup.Dice.Count; i++)
            {
                if (!cup.Dice[i].IsHeld)
                {
                    _dieViews[i].SetVisibility(true);
                    _dieViews[i].ResetToIdleSilent();
                }
            }

            float elapsed = 0f;
            const float duration = 0.5f;
            const float frameStep = 0.05f;
            while (elapsed < duration)
            {
                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld)
                    {
                        _dieViews[i].UpdateView(UnityEngine.Random.Range(1, 7), false);
                    }
                }
                elapsed += frameStep;
                yield return new WaitForSeconds(frameStep);
            }

            for (int i = 0; i < cup.Dice.Count; i++)
            {
                if (!cup.Dice[i].IsHeld)
                {
                    _dieViews[i].UpdateView(cup.Dice[i].Value, false);
                }
            }
            yield return new WaitForSeconds(0.15f);
            // #region agent log
            AgentLog("post-fix", "H8", "GameController.PlayBotScoreboardRollRoutine", "bot scoreboard roll animation finished", $"{{\"rollsLeft\":{cup.RollsLeft}}}");
            // #endregion
        }

        // ==========================================
        //  CORE EVENTS & HELPERS
        // ==========================================
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

        private void StartVisualTurn(Player player)
        {
            SetScoreboardVisibility(true);

            LoadAndApplySkins();

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
                if (_diceCupView != null) _diceCupView.DisableInteraction();
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(true);
                // #region agent log
                AgentLog("pre-fix", "H2", "GameController.StartVisualTurn", "bot turn ui state", $"{{\"skipButtonSetActive\":{(_skipBotButton != null).ToString().ToLower()},\"skipButtonActuallyActive\":{(_skipBotButton != null && _skipBotButton.gameObject.activeSelf).ToString().ToLower()}}}");
                // #endregion

                var botInput = _playerInputs[player] as BotPlayerInput;
                botInput?.StartBotTurn(_matchManager.Cup, player.ScoreCard, _matchManager);
            }
            else
            {
                SetUIInteractable(true);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(false);
                if (_diceCupView != null) _diceCupView.EnableInteraction();
                _canStartRoll = true;
            }
        }

       private void LoadAndApplySkins()
        {
            string equippedDiceId = PlayerPrefsEconomyService.Instance.EquippedDiceId;
            
            ShopItemConfig itemConfig = Resources.Load<ShopItemConfig>($"ShopItems/{equippedDiceId}/{equippedDiceId}_ShopItem");

            if (itemConfig == null || itemConfig.DiceSkin == null) 
            {
                itemConfig = Resources.Load<ShopItemConfig>("ShopItems/dice_default/dice_default_ShopItem");
            }

            if (itemConfig != null && itemConfig.DiceSkin != null)
            {
                foreach (var dieView in _dieViews)
                {
                    dieView.SetSkin(itemConfig.DiceSkin.Faces);
                }
            }
        }

        private Vector3 GetValidScatterPosition(List<Vector3> usedPositions)
        {
            if (_scatterArea == null) return Vector3.zero;

            Vector3 finalWorldPos = Vector3.zero;
            bool foundValidSpot = false;

            float padding = 60f;
            float minX = _scatterArea.rect.xMin + padding;
            float maxX = _scatterArea.rect.xMax - padding;
            float minY = _scatterArea.rect.yMin + padding;
            float maxY = _scatterArea.rect.yMax - padding;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                float randX = UnityEngine.Random.Range(minX, maxX);
                float randY = UnityEngine.Random.Range(minY, maxY);
                Vector3 randomLocalPos = new Vector3(randX, randY, 0f);

                foundValidSpot = true;

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
                    finalWorldPos = _scatterArea.TransformPoint(randomLocalPos);
                    break;
                }
            }

            return finalWorldPos;
        }

        private void HandleDieStateChanged(int index, bool isHeld)
        {
            int currentValue = _matchManager.Cup.Dice[index].Value;
            _dieViews[index].UpdateView(currentValue, isHeld);
            _dieViews[index].PlayToggleAnimation(isHeld);
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
            
            Player localPlayer = rankings.FirstOrDefault(p => !p.IsBot);
            if (localPlayer != null)
            {
                int rewardCoins = Mathf.Max(10, localPlayer.ScoreCard.GrandTotal / 10); 
                PlayerPrefsEconomyService.Instance.PlayerWallet.AddCoins(rewardCoins);
            }

            if (_gameOverView == null) return;

            if (rankings.Count == 1) _gameOverView.ShowSinglePlayer(rankings[0].ScoreCard.GrandTotal);
            else _gameOverView.ShowMultiPlayer(rankings);
        }

        private void HandleSkipBotClicked()
        {
            if (_matchManager.CurrentPlayer.IsBot)
            {
                // #region agent log
                AgentLog("pre-fix", "H3", "GameController.HandleSkipBotClicked", "skip button clicked during bot turn", $"{{\"isDiceRolling\":{_isDiceRolling.ToString().ToLower()},\"rollsLeft\":{_matchManager.Cup.RollsLeft}}}");
                // #endregion
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
            _scoreCardView.ClearAllHighlights();

            if (_currentPlayerNameText != null)
            {
                if (_matchManager.Players.Count == 1)
                {
                    int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);
                    int currentScore = player.ScoreCard.GrandTotal;

                    if (currentScore > currentHighScore && currentHighScore > 0)
                    {
                        // Zur Erinnerung: Stelle sicher, dass "new_record" in deinem Lokalisierungs-CSV gepflegt ist!
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("new_record", currentScore);
                        _currentPlayerNameText.color = Color.green;
                    }
                    else
                    {
                        // Zur Erinnerung: Stelle sicher, dass "high_score" in deinem Lokalisierungs-CSV gepflegt ist!
                        int displayScore = Mathf.Max(currentHighScore, currentScore);
                        _currentPlayerNameText.text = LocalizationService.Instance.GetText("high_score", displayScore);
                        _currentPlayerNameText.color = Color.yellow;
                    }
                }
                else
                {
                    // Zur Erinnerung: Stelle sicher, dass "turn_indicator" in deinem Lokalisierungs-CSV gepflegt ist!
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

        // ==========================================
        //  SETTINGS & SCENE MANAGEMENT
        // ==========================================
        public void OpenSettings() 
        {
            if (_settingsPanel != null)
            {
                StartCoroutine(OpenSettingsCoroutine());
            }
        }

        private IEnumerator OpenSettingsCoroutine()
        {
            _settingsPanel.SetActive(true);
            yield return null;
            if (_settingsAnimator != null) _settingsAnimator.SetBool("IsVisible", true);
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

            if (_diceCupView != null) _diceCupView.OnCupTouched -= HandleCupDragToRoll;
        }

        private void HandleLanguageChanged()
        {
            RefreshUIForCurrentPlayer(_matchManager.CurrentPlayer);
            _scoreCardView.UpdateTranslations();
            _scoreCardView.RefreshDisplay(_matchManager.CurrentPlayer.ScoreCard);
        }

        // ==========================================
        //  TURN TRANSITIONS
        // ==========================================
        private void HandleTurnEnded(Player playerWhoJustFinished)
        {
            if (_isTransitioningTurn) return;

            _isTransitioningTurn = true;
            SetUIInteractable(false);
            UpdateMainActionUI();

            StartCoroutine(TurnTransitionRoutine(playerWhoJustFinished));
        }

        private IEnumerator TurnTransitionRoutine(Player playerWhoJustFinished)
        {
            float delay = playerWhoJustFinished.IsBot ? 1.5f : 0.8f;
            yield return new WaitForSeconds(delay);

            ResetAllDiceVisuals();
            yield return new WaitForSeconds(0.2f);

            if (_matchManager != null) _matchManager.AdvanceToNextTurn();
            _isTransitioningTurn = false;
        }

    }
}