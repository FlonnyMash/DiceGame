using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Controllers;
using DiceGame.Core.AI;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Models;
using DiceGame.Core.Networking;
using DiceGame.Core.Rules;
using DiceGame.Core.Systems;

namespace DiceGame.Infrastructure.Networking.Testing
{
    // Editor-only smoke-test scaffold: impersonates a remote peer's commands so the
    // lockstep pipeline (serialize -> loopback -> deserialize -> NetworkPlayerInput
    // -> MatchManager) runs end-to-end inside a single Editor instance.
    //
    // Reads MatchManager/Cup/ScoreCard purely as inputs to BotLogic; all state
    // mutation happens through INetworkService.SendAction with PlayerId set to the
    // impersonated remote ID, so the local director routes packets to the matching
    // NetworkPlayerInput just as a real peer would.
    public class FakeRemotePeer : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private GameController _gameController;

        [Header("Impersonation")]
        [Tooltip("Remote PlayerIds this peer will drive. Local player id is always skipped.")]
        [SerializeField] private List<int> _impersonatedPlayerIds = new List<int> { 1 };

        [Header("Simulated Thinking")]
        [SerializeField, Min(0f)] private float _minActionDelay = 0.5f;
        [SerializeField, Min(0f)] private float _maxActionDelay = 1.0f;
        [SerializeField, Min(0f)] private float _interToggleDelay = 0.1f;
        [SerializeField, Min(0f)] private float _postBonusDelay = 0.4f;

        private MatchManager _matchManager;
        private INetworkService _networkService;
        private Coroutine _activeRoutine;
        private bool _hooked;

        private void Start()
        {
            if (_gameController == null) _gameController = FindObjectOfType<GameController>();
            if (_gameController == null)
            {
                Debug.LogWarning("[FakeRemotePeer] No GameController in scene; disabling.");
                enabled = false;
                return;
            }

            _gameController.OnOnlineMatchReady += HookIntoMatch;

            // Defensive: handshake may have already finished if order-of-init drifts.
            if (_gameController.MatchManager != null && _gameController.NetworkService != null)
            {
                HookIntoMatch(_gameController.MatchManager, _gameController.NetworkService);
            }
        }

        private void HookIntoMatch(MatchManager match, INetworkService network)
        {
            if (_hooked) return;
            _matchManager = match;
            _networkService = network;
            _matchManager.OnTurnStarted += HandleTurnStarted;
            _hooked = true;
        }

        private void OnDestroy()
        {
            if (_gameController != null) _gameController.OnOnlineMatchReady -= HookIntoMatch;
            if (_matchManager != null) _matchManager.OnTurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted(Player player)
        {
            if (!enabled || player == null) return;
            if (_matchManager == null || _networkService == null) return;
            if (!_impersonatedPlayerIds.Contains(player.Id)) return;
            if (player.Id == _networkService.LocalPlayerId) return;

            if (_activeRoutine != null) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(RunFakeRemoteTurn(player));
        }

        private IEnumerator RunFakeRemoteTurn(Player player)
        {
            yield return new WaitForSeconds(NextDelay());

            DiceCup cup = _matchManager.Cup;
            ScoreCard scoreCard = player.ScoreCard;

            for (int r = 0; r < DiceCup.MaxRolls; r++)
            {
                int rollsBefore = cup.RollsLeft;
                Send(PlayerActionType.Roll, player.Id);

                while (cup.RollsLeft == rollsBefore) yield return null;
                while (_matchManager.IsRollInProgress) yield return null;

                if (r == DiceCup.MaxRolls - 1) break;

                yield return new WaitForSeconds(NextDelay());

                List<int> desiredHolds = BotLogic.GetDiceToHold(cup.Dice, scoreCard);

                for (int i = 0; i < cup.Dice.Count; i++)
                {
                    bool shouldHold = desiredHolds.Contains(i);
                    if (cup.Dice[i].IsHeld != shouldHold)
                    {
                        Send(PlayerActionType.ToggleHold, player.Id, i);
                        if (_interToggleDelay > 0f) yield return new WaitForSeconds(_interToggleDelay);
                    }
                }

                if (desiredHolds.Count == 5) break;
            }

            yield return new WaitForSeconds(NextDelay());

            if (scoreCard.UpperSectionRaw >= 63 && !scoreCard.IsBonusClaimed)
            {
                Send(PlayerActionType.BonusClaim, player.Id);
                if (_postBonusDelay > 0f) yield return new WaitForSeconds(_postBonusDelay);
            }

            ScoreCategory category = BotLogic.ChooseBestCategory(scoreCard, cup.Dice);
            Send(PlayerActionType.Category, player.Id, (int)category);
        }

        private void Send(PlayerActionType type, int playerId, int payload = 0)
        {
            if (_networkService == null) return;
            if (_networkService.Status != NetworkStatus.Connected) return;
            var action = new PlayerAction(type, playerId, payload);
            _networkService.SendAction(action.Serialize());
        }

        private float NextDelay()
            => UnityEngine.Random.Range(_minActionDelay, Mathf.Max(_minActionDelay, _maxActionDelay));
    }
}
