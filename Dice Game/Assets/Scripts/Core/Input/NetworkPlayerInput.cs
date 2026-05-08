using System;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Networking;
using DiceGame.Core.Rules;

namespace DiceGame.Core.Inputs
{
    // Mirrors LocalPlayerInput / BotPlayerInput, but the "trigger" comes from the wire
    // (NetworkSessionDirector calls EnqueueRemoteAction). One instance per remote player.
    public class NetworkPlayerInput : MonoBehaviour, IPlayerInput
    {
        public event Action OnRollRequested;
        public event Action<int> OnToggleHoldRequested;
        public event Action<ScoreCategory> OnCategoryRequested;
        public event Action OnBonusClaimRequested;

        public int PlayerId { get; private set; } = -1;

        private bool _isActive;
        private readonly Queue<PlayerAction> _pendingActions = new Queue<PlayerAction>();

        public void Configure(int playerId)
        {
            PlayerId = playerId;
        }

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
            if (_isActive)
            {
                DrainPendingActions();
            }
        }

        // Called by NetworkSessionDirector when an action targeting this player arrives over the wire.
        // While inactive (i.e. it's not this player's turn yet according to the local MatchManager),
        // we buffer so we can replay in order as soon as the turn rotates to us.
        public void EnqueueRemoteAction(PlayerAction action)
        {
            if (action.PlayerId != PlayerId) return;

            if (_isActive)
            {
                Dispatch(action);
            }
            else
            {
                _pendingActions.Enqueue(action);
            }
        }

        private void DrainPendingActions()
        {
            while (_pendingActions.Count > 0)
            {
                Dispatch(_pendingActions.Dequeue());
            }
        }

        private void Dispatch(PlayerAction action)
        {
            switch (action.Type)
            {
                case PlayerActionType.Roll:
                    OnRollRequested?.Invoke();
                    break;
                case PlayerActionType.ToggleHold:
                    OnToggleHoldRequested?.Invoke(action.Payload);
                    break;
                case PlayerActionType.Category:
                    OnCategoryRequested?.Invoke((ScoreCategory)action.Payload);
                    break;
                case PlayerActionType.BonusClaim:
                    OnBonusClaimRequested?.Invoke();
                    break;
            }
        }
    }
}
