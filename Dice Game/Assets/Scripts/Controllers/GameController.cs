using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;
using DiceGame.UI.Views;
using DiceGame.Audio;
using DiceGame.Core.Systems;
using DiceGame.Core.Inputs;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Networking;
using DiceGame.Services;
using DiceGame.UI.Effects;
using DiceGame.Configs;
using DiceGame.Infrastructure.Networking.Ugs;
using DiceGame.Networking;

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

        [Header("Online Multiplayer (Phase 2C)")]
        [Tooltip("Localized status overlay used for connecting / desync / disconnect messages.")]
        [SerializeField] private ConnectionStatusOverlay _connectionStatusOverlay;
        [Tooltip("Optional: configure the lockstep hash-gate timeouts. Leave null to spawn one at runtime with defaults.")]
        [SerializeField] private LockstepHashGate _lockstepHashGate;

        private MatchManager _matchManager;
        private LocalPlayerInput _localInput;
        private Dictionary<Player, IPlayerInput> _playerInputs;
        private Player _previousPlayer;

        // Online multiplayer (only populated when MatchData.IsOnline is true).
        private NetworkSessionDirector _sessionDirector;
        private INetworkService _onlineTransport;
        private List<Player> _pendingPlayers;
        private bool _hasBeenConnected;
        private bool _isMatchAborted;

        // Public hooks for scene-side test scaffolding. The runtime-only MatchManager and
        // INetworkService cannot be wired through the Inspector, so we expose them via getters
        // + a one-shot ready event fired right after the seed handshake.
        public MatchManager MatchManager => _matchManager;
        public INetworkService NetworkService => _onlineTransport;
        public event Action<MatchManager, INetworkService> OnOnlineMatchReady;

        private bool _isTransitioningTurn = false;
        private bool _isDiceRolling = false;
        private bool _isScoreboardVisibleInternal = true;
        private bool _canStartRoll = true;

        private void Start()
        {
            _settingsPanel.SetActive(false);
            if (_passDeviceView != null) _passDeviceView.Hide();
            if (_gameOverView != null) _gameOverView.Hide();
            if (_connectionStatusOverlay != null) _connectionStatusOverlay.Hide();

            if (MatchData.IsOnline)
            {
                SetupOnlineSession();
            }
            else
            {
                SetupCoreGame();
                BindUIEvents();
                BindManagerEvents();
                _matchManager.StartGame();
            }

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
            for (int i = 0; i < MatchData.PlayerNames.Count; i++)
            {
                string name = MatchData.PlayerNames[i];
                bool isBot = name.Contains("Bot");
                players.Add(new Player(i, name, isBot, isRemote: false));
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

        // Online flow: rebind to the UgsNetworkTransport that the lobby attached to the persistent
        // NetworkManager GameObject. Build players + inputs synchronously, spin up the director,
        // then defer MatchManager creation until the seed handshake completes (HandleSeedReceived).
        private void SetupOnlineSession()
        {
            _pendingPlayers = BuildOnlinePlayers();

            _localInput = gameObject.AddComponent<LocalPlayerInput>();
            _playerInputs = new Dictionary<Player, IPlayerInput>();
            List<NetworkPlayerInput> remoteInputs = new List<NetworkPlayerInput>();

            foreach (var p in _pendingPlayers)
            {
                if (p.Id == MatchData.LocalPlayerId)
                {
                    _playerInputs.Add(p, _localInput);
                }
                else
                {
                    var remote = gameObject.AddComponent<NetworkPlayerInput>();
                    remote.Configure(p.Id);
                    _playerInputs.Add(p, remote);
                    remoteInputs.Add(remote);
                }
            }

            BindUIEvents();

            _onlineTransport = AcquirePersistentTransport();
            if (_onlineTransport == null)
            {
                Debug.LogError("[GameController] No UgsNetworkTransport found on the persistent NetworkManager. Did the lobby create one?");
                AbortMatch("err_connection_lost", broadcast: false);
                return;
            }

            _sessionDirector = gameObject.AddComponent<NetworkSessionDirector>();
            _sessionDirector.Configure(_onlineTransport, _localInput, remoteInputs);
            _sessionDirector.OnSeedReceived += HandleSeedReceived;
            _sessionDirector.OnStatusChanged += HandleOnlineStatusChanged;
            _sessionDirector.OnAbortReceived += HandleAbortReceived;
            _sessionDirector.BeginHandshake();

            // Host-only fast path: react to NGO peer disconnects in seconds rather than waiting
            // for the 10s lockstep hard-timeout. The host then broadcasts Abort so guests learn
            // the actual reason instead of timing out themselves.
            if (_onlineTransport is UgsNetworkTransport ugs && MatchData.IsHost)
            {
                ugs.OnPeerDisconnected += HandlePeerDisconnectedAsHost;
            }

            // Track ever-connected so a subsequent transient blip can be shown as
            // err_connection_lost rather than the initial msg_connecting message.
            _hasBeenConnected = _onlineTransport.Status == NetworkStatus.Connected;
            if (_connectionStatusOverlay != null && !_hasBeenConnected)
            {
                _connectionStatusOverlay.Show("msg_connecting");
            }
        }

        private INetworkService AcquirePersistentTransport()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            return nm.gameObject.GetComponent<UgsNetworkTransport>();
        }

        private List<Player> BuildOnlinePlayers()
        {
            List<Player> players = new List<Player>();
            int count = MatchData.PlayerNames.Count;

            for (int i = 0; i < count; i++)
            {
                string name = MatchData.PlayerNames[i];
                bool isRemote = i < MatchData.IsRemoteFlags.Count && MatchData.IsRemoteFlags[i];

                // Online lobby has no bots; remote-vs-local is the only distinction.
                players.Add(new Player(i, name, isBot: false, isRemote: isRemote));
            }

            return players;
        }

        private void HandleSeedReceived(int seed)
        {
            if (_matchManager != null) return;

            _matchManager = new MatchManager(_pendingPlayers, seed);
            _pendingPlayers = null;

            BindManagerEvents();
            ConfigureLockstepGate();

            OnOnlineMatchReady?.Invoke(_matchManager, _onlineTransport);
            _matchManager.StartGame();
        }

        private void ConfigureLockstepGate()
        {
            if (_lockstepHashGate == null)
            {
                _lockstepHashGate = gameObject.AddComponent<LockstepHashGate>();
            }
            _lockstepHashGate.Configure(_onlineTransport, _matchManager, MatchData.PlayerNames.Count, MatchData.IsHost);
            _lockstepHashGate.Begin();

            _lockstepHashGate.OnSyncStalling += HandleSyncStalling;
            _lockstepHashGate.OnDesyncDetected += HandleDesyncDetected;
            _lockstepHashGate.OnSyncFailed += HandleSyncFailed;

            // The director surfaces the wire packets; the gate consumes them.
            _sessionDirector.OnStateHashReceived += _lockstepHashGate.HandleStateHash;
            _sessionDirector.OnSyncOkReceived += _lockstepHashGate.HandleSyncOk;
        }

        private void HandleOnlineStatusChanged(NetworkStatus status)
        {
            if (_isMatchAborted) return;

            if (status == NetworkStatus.Connected) _hasBeenConnected = true;

            if (_connectionStatusOverlay != null)
            {
                _connectionStatusOverlay.ApplyStatus(status, _hasBeenConnected);
            }

            if (_hasBeenConnected && (status == NetworkStatus.Disconnected || status == NetworkStatus.Error))
            {
                AbortMatch("err_connection_lost", broadcast: false);
            }
        }

        private void HandleAbortReceived(AbortReason reason)
        {
            if (_isMatchAborted) return;
            string key = reason == AbortReason.Desync ? "err_desync_detected" : "err_connection_lost";
            AbortMatch(key, broadcast: false);
        }

        private void HandlePeerDisconnectedAsHost(ulong _)
        {
            if (_isMatchAborted) return;
            AbortMatch("err_connection_lost", broadcast: true, reason: AbortReason.PeerDrop);
        }

        private void HandleSyncStalling(int turnIndex)
        {
            if (_isMatchAborted) return;
            if (_connectionStatusOverlay != null) _connectionStatusOverlay.Show("err_connection_unstable");
        }

        private void HandleDesyncDetected(int turnIndex)
        {
            Debug.LogError($"[GameController] Desync detected at turnIndex={turnIndex}. Aborting match.");
            AbortMatch("err_desync_detected", broadcast: true, reason: AbortReason.Desync);
        }

        private void HandleSyncFailed(int turnIndex)
        {
            Debug.LogError($"[GameController] Hard sync timeout at turnIndex={turnIndex}. Aborting match.");
            AbortMatch("err_connection_lost", broadcast: true, reason: AbortReason.PeerDrop);
        }

        // --- Match abort -------------------------------------------------------------------

        private void AbortMatch(string locKey, bool broadcast, AbortReason reason = AbortReason.Unknown)
        {
            if (_isMatchAborted) return;
            _isMatchAborted = true;

            // Best-effort: tell other peers we're going away.
            if (broadcast && _sessionDirector != null && _onlineTransport != null
                && _onlineTransport.Status == NetworkStatus.Connected)
            {
                _sessionDirector.BroadcastAbort(reason);
            }

            // Detach inputs so no further commands are accepted, locally or remote.
            if (_playerInputs != null)
            {
                foreach (var input in _playerInputs.Values) input?.SetActive(false);
            }
            if (_matchManager != null)
            {
                _matchManager.AttachInput(null);
            }

            // Stop coroutines to unfreeze the turn-transition state cleanly. Anything that was
            // mid-animation will simply leave the UI in whatever state it had; the overlay covers
            // it visually.
            StopAllCoroutines();
            SetUIInteractable(false);

            if (_connectionStatusOverlay != null)
            {
                _connectionStatusOverlay.OnBackToMenuClicked -= HandleAbortBackToMenu;
                _connectionStatusOverlay.OnBackToMenuClicked += HandleAbortBackToMenu;
                _connectionStatusOverlay.ShowTerminal(locKey);
            }

            // Auto-return after a few seconds in case the user doesn't tap the button.
            StartCoroutine(AutoReturnAfterAbort(4f));
        }

        private IEnumerator AutoReturnAfterAbort(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            HandleAbortBackToMenu();
        }

        private void HandleAbortBackToMenu()
        {
            // Stopping the gate also prevents stray "stalling" events from racing the scene unload.
            if (_lockstepHashGate != null) _lockstepHashGate.Stop();

            // Tear down the persistent transport so the next match can re-create it. The
            // transport's own OnDisable runs nm.Shutdown(true) + LeaveAsync, which feeds into the
            // static s_PreviousShutdownTask coordinator.
            if (_onlineTransport is MonoBehaviour mb && mb != null)
            {
                Destroy(mb);
            }

            MatchData.ResetToOffline();
            SceneManager.LoadScene("MainMenuScene");
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

            bool isLocalHuman = IsLocalHuman(_matchManager.CurrentPlayer);

            _mainActionButton.interactable = isLocalHuman && !_isDiceRolling && !_isTransitioningTurn;

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
            if (_matchManager.Cup.RollsLeft <= 0) return;
            if (!IsLocalHuman(_matchManager.CurrentPlayer)) return;

            _canStartRoll = false;
            _localInput.TriggerRoll();
        }

        private static bool IsLocalHuman(Player player)
            => player != null && !player.IsBot && !player.IsRemote;

        private void HandleDiceRolled(DiceCup cup)
        {
            StartCoroutine(RollAnimationRoutine(cup));
        }

        private IEnumerator RollAnimationRoutine(DiceCup cup)
        {
            _isDiceRolling = true;
            UpdateMainActionUI();

            _scoreCardView.ClearAllPotentials();
            _scoreCardView.ClearAllHighlights();
            if (_hintView != null) _hintView.HideHint();
            if (_diceCanvasGroup != null) _diceCanvasGroup.blocksRaycasts = false;

            bool isLocalHuman = IsLocalHuman(_matchManager.CurrentPlayer);
            // Bots and remote peers both run a non-interactive quick reroll on the scoreboard.
            // Pure presentation -- dice values are already deterministic before the animation.
            if (!isLocalHuman)
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

            bool allDiceHeld = cup.Dice.All(die => die.IsHeld);

            if (!allDiceHeld)
            {
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

                    if (isLocalHuman) {
                        _diceCupView.EnableInteraction();
                        while (!shakeFinished) yield return null;
                    } else {
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

                    // Presentation-only: scatter rotation uses Unity's global RNG, NOT the
                    // deterministic dice RNG. Visuals can differ between peers; state cannot.
                    float randomRot = UnityEngine.Random.Range(0f, 360f);
                    _dieViews[i].UpdateView(cup.Dice[i].Value, false);
                    _dieViews[i].ScatterWorld(finalPos, randomRot);
                }
            }

            if (!allDiceHeld)
            {
                if (_rollDiceSounds != null && _rollDiceSounds.Length > 0 && isLocalHuman)
                {
                    AudioManager.Instance.PlaySFX(_rollDiceSounds[UnityEngine.Random.Range(0, _rollDiceSounds.Length)]);
                }

                if (_diceCupView != null) yield return _diceCupView.AnimateRevealRoutine();

                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    if (!cup.Dice[i].IsHeld) _dieViews[i].SetVisibility(true);
                }

                // Presentation-only deterministic timer; state convergence already handled by
                // NetworkPlayerInput.EnqueueRemoteAction (which buffers commands while inactive).
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
            _matchManager.NotifyRollAnimationCompleted();

            SetScoreboardVisibility(true);

            if (_diceCupView != null)
            {
                _diceCupView.ResetCup();
                if (isLocalHuman && cup.RollsLeft > 0)
                {
                    _diceCupView.EnableInteraction();
                }
            }

            UpdatePotentialScores(cup, _matchManager.CurrentPlayer);

            if (isLocalHuman && _hintView != null)
            {
                ScoreCategory? bestHint = HintCalculator.GetBestHint(_matchManager.CurrentPlayer.ScoreCard, cup.Dice, cup.RollsLeft);
                if (bestHint.HasValue) _hintView.ShowHint(bestHint.Value);
            }

            if (_diceCanvasGroup != null && isLocalHuman) _diceCanvasGroup.blocksRaycasts = true;

            UpdateMainActionUI();
        }

        private IEnumerator PlayBotScoreboardRollRoutine(DiceCup cup)
        {
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
                        // Presentation-only: face flicker uses Unity's global RNG (not dice RNG).
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
                bool wasHuman = IsLocalHuman(_previousPlayer);
                bool isNextHuman = IsLocalHuman(player);
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

                var botInput = _playerInputs[player] as BotPlayerInput;
                botInput?.StartBotTurn(_matchManager.Cup, player.ScoreCard, _matchManager);
            }
            else
            {
                bool isLocalHuman = IsLocalHuman(player);
                SetUIInteractable(isLocalHuman);
                if (_skipBotButton != null) _skipBotButton.gameObject.SetActive(false);
                if (_diceCupView != null)
                {
                    if (isLocalHuman) _diceCupView.EnableInteraction();
                    else _diceCupView.DisableInteraction();
                }
                _canStartRoll = isLocalHuman;
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

            // Stop the gate so we don't fire stalling events during the game-over screen.
            if (_lockstepHashGate != null) _lockstepHashGate.Stop();

            Player localPlayer = rankings.FirstOrDefault(IsLocalHuman);
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

        public void GoToMainMenu()
        {
            // Calling this in the middle of an online match would orphan the transport, so the
            // safe path is to drive the explicit abort flow when we're online.
            if (MatchData.IsOnline)
            {
                AbortMatch("err_connection_lost", broadcast: true, reason: AbortReason.UserQuit);
                return;
            }
            SceneManager.LoadScene("MainMenuScene");
        }

        private void HandleRestart()
        {
            // Restart only makes sense in offline mode; online matches must be re-lobbied.
            if (MatchData.IsOnline)
            {
                AbortMatch("err_connection_lost", broadcast: true, reason: AbortReason.UserQuit);
                return;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void HandleMainMenu()
        {
            if (MatchData.IsOnline)
            {
                AbortMatch("err_connection_lost", broadcast: true, reason: AbortReason.UserQuit);
                return;
            }
            SceneManager.LoadScene("MainMenuScene");
        }

        private void OnDestroy()
        {
            if (_mainActionButton != null) _mainActionButton.onClick.RemoveListener(HandleMainAction);
            LocalizationService.Instance.OnLanguageChanged -= HandleLanguageChanged;
            if (_matchManager != null) _matchManager.OnTurnEnded -= HandleTurnEnded;
            if (_sessionDirector != null)
            {
                _sessionDirector.OnSeedReceived -= HandleSeedReceived;
                _sessionDirector.OnStatusChanged -= HandleOnlineStatusChanged;
                _sessionDirector.OnAbortReceived -= HandleAbortReceived;
                if (_lockstepHashGate != null)
                {
                    _sessionDirector.OnStateHashReceived -= _lockstepHashGate.HandleStateHash;
                    _sessionDirector.OnSyncOkReceived -= _lockstepHashGate.HandleSyncOk;
                }
            }
            if (_lockstepHashGate != null)
            {
                _lockstepHashGate.OnSyncStalling -= HandleSyncStalling;
                _lockstepHashGate.OnDesyncDetected -= HandleDesyncDetected;
                _lockstepHashGate.OnSyncFailed -= HandleSyncFailed;
            }
            if (_onlineTransport is UgsNetworkTransport ugs)
            {
                ugs.OnPeerDisconnected -= HandlePeerDisconnectedAsHost;
            }
            if (_connectionStatusOverlay != null)
            {
                _connectionStatusOverlay.OnBackToMenuClicked -= HandleAbortBackToMenu;
            }
            if (_diceCupView != null) _diceCupView.OnCupTouched -= HandleCupDragToRoll;
        }

        private void HandleLanguageChanged()
        {
            if (_matchManager == null || _matchManager.CurrentPlayer == null) return;
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
            // Online players have IsBot=false so this is always 0.8s online; the longer 1.5s
            // bot-pause only applies offline. Both branches are deterministic timers.
            float delay = playerWhoJustFinished.IsBot ? 1.5f : 0.8f;
            yield return new WaitForSeconds(delay);

            ResetAllDiceVisuals();
            yield return new WaitForSeconds(0.2f);

            if (_matchManager == null) yield break;

            // Online mode: the lockstep hash gate must clear this turnIndex (i.e. host has seen
            // matching hashes from every peer and broadcast SyncOk) before we may rotate the wheel.
            // Net round-trip is typically masked by the 0.8s + 0.2s above; if not, the
            // ConnectionStatusOverlay is already showing err_connection_unstable / msg_connecting.
            if (MatchData.IsOnline && _lockstepHashGate != null)
            {
                int currentTurnIndex = _matchManager.TurnIndex;
                while (!_lockstepHashGate.IsCleared(currentTurnIndex)
                       && !_lockstepHashGate.HasFailed
                       && !_isMatchAborted)
                {
                    yield return null;
                }
                if (_lockstepHashGate.HasFailed || _isMatchAborted)
                {
                    // The gate already fired OnSyncFailed / OnDesyncDetected which triggered
                    // AbortMatch via our handler; just exit cleanly.
                    yield break;
                }
            }

            _matchManager.AdvanceToNextTurn();
            _isTransitioningTurn = false;
        }

    }
}
