using System;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Rules;

namespace DiceGame.Core.Inputs
{
    public class LocalPlayerInput : MonoBehaviour, IPlayerInput
    {
        public event Action OnRollRequested;
        public event Action<int> OnToggleHoldRequested;
        public event Action<ScoreCategory> OnCategoryRequested;
        public event Action OnBonusClaimRequested;

        // Diese Methode rufen wir gleich vom UI-Button aus auf
        public void TriggerBonusClaimed()
        {
            if (_isActive) 
            {
                OnBonusClaimRequested?.Invoke();
            }
        }

        private bool _isActive = false;

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }

        // --- Diese Methoden werden später von deiner UI (Buttons) aufgerufen ---

        public void TriggerRoll()
        {
            if (_isActive) 
            {
                OnRollRequested?.Invoke();
            }
        }

        public void TriggerToggleHold(int dieIndex)
        {
            if (_isActive) 
            {
                OnToggleHoldRequested?.Invoke(dieIndex);
            }
        }

        public void TriggerCategorySelected(ScoreCategory category)
        {
            if (_isActive) 
            {
                OnCategoryRequested?.Invoke(category);
            }
        }
    }
}