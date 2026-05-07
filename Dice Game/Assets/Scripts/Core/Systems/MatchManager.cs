using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;
using DiceGame.Core.Interfaces;

namespace DiceGame.Core.Systems
{
    public class MatchManager
    {
        public DiceCup Cup { get; private set; }
        public List<Player> Players { get; private set; }
        public int CurrentPlayerIndex { get; private set; }
        
        public Player CurrentPlayer => Players[CurrentPlayerIndex];

        // --- EVENTS FÜR DIE UI ---
        // Die UI abonniert diese Events, um Grafiken und Animationen zu steuern
        public event Action<Player> OnTurnStarted;
        public event Action<Player> OnTurnEnded;
        public event Action<DiceCup> OnDiceRolled;
        public event Action<int, bool> OnDieStateChanged; // (dieIndex, isHeld)
        public event Action<Player, ScoreCategory, int> OnScoreApplied;
        public event Action<Player> OnBonusClaimed;
        public event Action<List<Player>> OnGameOver;

        private IPlayerInput _currentPlayerInput;
        private bool _isRollInProgress;
        public bool IsRollInProgress => _isRollInProgress;
        
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

        public MatchManager(List<Player> players)
        {
            Players = players;
            Cup = new DiceCup();
        }

        public void StartGame()
        {
            CurrentPlayerIndex = 0;
            StartTurn();
        }

        private void StartTurn()
        {
            _isRollInProgress = false;
            Cup.ResetTurn();
            OnTurnStarted?.Invoke(CurrentPlayer);
        }

        // Wird von der Szene aufgerufen, um den Input (Lokal, Bot, Netzwerk) an die Logik zu koppeln
        public void AttachInput(IPlayerInput input)
        {
            // Alten Input abklemmen (Sicherheit)
            if (_currentPlayerInput != null)
            {
                _currentPlayerInput.OnRollRequested -= HandleRollRequested;
                _currentPlayerInput.OnToggleHoldRequested -= HandleToggleHoldRequested;
                _currentPlayerInput.OnCategoryRequested -= HandleCategoryRequested;
                _currentPlayerInput.OnBonusClaimRequested -= HandleBonusClaimRequested;
            }

            _currentPlayerInput = input;
            
            // Neuen Input anklemmen
            if (_currentPlayerInput != null)
            {
                _currentPlayerInput.OnRollRequested += HandleRollRequested;
                _currentPlayerInput.OnToggleHoldRequested += HandleToggleHoldRequested;
                _currentPlayerInput.OnCategoryRequested += HandleCategoryRequested;
                _currentPlayerInput.OnBonusClaimRequested += HandleBonusClaimRequested;
            }
        }

        // --- INPUT HANDLER ---
        // Hier reagiert die Logik auf die Wünsche des Spielers/Bots

        private void HandleRollRequested()
        {
            if (_isRollInProgress)
            {
                // #region agent log
                AgentLog("post-fix", "H1", "MatchManager.HandleRollRequested", "roll ignored because animation still running", $"{{\"currentPlayer\":\"{CurrentPlayer.Name}\",\"rollsLeft\":{Cup.RollsLeft}}}");
                // #endregion
                return;
            }

            // #region agent log
            AgentLog("pre-fix", "H1", "MatchManager.HandleRollRequested", "roll request received", $"{{\"currentPlayer\":\"{CurrentPlayer.Name}\",\"isBot\":{CurrentPlayer.IsBot.ToString().ToLower()},\"rollsLeftBefore\":{Cup.RollsLeft}}}");
            // #endregion
            if (Cup.Roll())
            {
                _isRollInProgress = true;
                // #region agent log
                AgentLog("pre-fix", "H1", "MatchManager.HandleRollRequested", "roll executed", $"{{\"rollsLeftAfter\":{Cup.RollsLeft}}}");
                // #endregion
                OnDiceRolled?.Invoke(Cup);
            }
        }

        public void NotifyRollAnimationCompleted()
        {
            _isRollInProgress = false;
            // #region agent log
            AgentLog("post-fix", "H1", "MatchManager.NotifyRollAnimationCompleted", "roll animation acknowledged complete", $"{{\"currentPlayer\":\"{CurrentPlayer.Name}\",\"rollsLeft\":{Cup.RollsLeft}}}");
            // #endregion
        }

        private void HandleToggleHoldRequested(int dieIndex)
        {
            if (Cup.RollsLeft < DiceCup.MaxRolls)
            {
                Cup.Dice[dieIndex].ToggleHold();
                OnDieStateChanged?.Invoke(dieIndex, Cup.Dice[dieIndex].IsHeld);
            }
        }

        private void HandleBonusClaimRequested()
        {
            if (CurrentPlayer.ScoreCard.IsBonusEligible && !CurrentPlayer.ScoreCard.IsBonusClaimed)
            {
                CurrentPlayer.ScoreCard.ClaimBonus();
                OnBonusClaimed?.Invoke(CurrentPlayer);
            }
        }

        private void HandleCategoryRequested(ScoreCategory category)
        {
            // #region agent log
            AgentLog("pre-fix", "H4", "MatchManager.HandleCategoryRequested", "category request received", $"{{\"category\":{(int)category},\"rollsLeft\":{Cup.RollsLeft}}}");
            // #endregion
            if (Cup.RollsLeft == DiceCup.MaxRolls) return; // Ohne Wurf keine Punkte!

            int points = ScoreCalculator.CalculateScore(Cup.Dice, category);
            
            // SetScore gibt 'true' zurück, wenn das Feld noch leer war
            if (CurrentPlayer.ScoreCard.SetScore(category, points))
            {
                OnScoreApplied?.Invoke(CurrentPlayer, category, points);
                CheckGameEndOrNextTurn();
            }
        }

        private void CheckGameEndOrNextTurn()
        {
            if (Players.All(p => p.ScoreCard.IsComplete))
            {
                var rankings = Players.OrderByDescending(p => p.ScoreCard.GrandTotal).ToList();
                OnGameOver?.Invoke(rankings);
            }
            else
            {
                // HIER IST DIE ÄNDERUNG: 
                // Wir starten NICHT mehr sofort den neuen Zug!
                // Wir rufen das Event auf, damit der GameController seine Pause starten kann.
                OnTurnEnded?.Invoke(CurrentPlayer);
            }
        }

        // NEU: Diese Methode ist public. Der GameController ruft sie auf, 
        // sobald seine Pause (Coroutine) und die Animationen fertig sind!
        public void AdvanceToNextTurn()
        {
            CurrentPlayerIndex = (CurrentPlayerIndex + 1) % Players.Count;
            StartTurn();
        }
    }
}