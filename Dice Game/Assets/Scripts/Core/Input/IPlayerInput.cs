using System;
using DiceGame.Core.Rules;

namespace DiceGame.Core.Interfaces
{
    public interface IPlayerInput
    {
        event Action OnRollRequested;
        event Action<int> OnToggleHoldRequested;
        event Action<ScoreCategory> OnCategoryRequested;
        
        // NEU: Aktion für den Bonus-Button
        event Action OnBonusClaimRequested; 

        void SetActive(bool isActive);
    }
}