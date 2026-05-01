using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Core.Interfaces;
using DiceGame.Core.Rules;
using DiceGame.Core.Models;
using DiceGame.Core.AI;

namespace DiceGame.Core.Inputs
{
    public class BotPlayerInput : MonoBehaviour, IPlayerInput
    {
        public event Action OnRollRequested;
        public event Action<int> OnToggleHoldRequested;
        public event Action<ScoreCategory> OnCategoryRequested;
        public event Action OnBonusClaimRequested;

        private bool _isActive = false;
        private Coroutine _botRoutine;
        private bool _isSkipping = false;

        public void SetActive(bool isActive)
        {
            _isActive = isActive;
        }

        // Wird vom MatchManager aufgerufen, wenn der Bot dran ist
        public void StartBotTurn(DiceCup currentCup, ScoreCard currentScoreCard)
        {
            if (!_isActive) return;

            _isSkipping = false;
            if (_botRoutine != null) StopCoroutine(_botRoutine);
            _botRoutine = StartCoroutine(RunBotRoutine(currentCup, currentScoreCard));
        }

        public void SkipBotTurn(DiceCup currentCup, ScoreCard currentScoreCard)
        {
            if (_isSkipping || !_isActive) return;
            _isSkipping = true;

            if (_botRoutine != null) StopCoroutine(_botRoutine);
            
            // Ohne Coroutine sofort zu Ende rechnen
            FastForwardRolls(currentCup, currentScoreCard);
        }

        private void FastForwardRolls(DiceCup cup, ScoreCard scoreCard)
        {
            int emergencyBreak = 0; 
            while (cup.RollsLeft > 0 && emergencyBreak < 3)
            {
                emergencyBreak++;
                List<int> diceToHold = BotLogic.GetDiceToHold(cup.Dice, scoreCard);
                if (diceToHold.Count == 5) break; 

                // Würfel virtuell halten
                foreach (int index in diceToHold)
                {
                    if (!cup.Dice[index].IsHeld)
                    {
                        OnToggleHoldRequested?.Invoke(index);
                    }
                }
                OnRollRequested?.Invoke(); 
            }

            // Bonus checken
            if (scoreCard.UpperSectionRaw >= 63 && !scoreCard.IsBonusClaimed)
            {
                OnBonusClaimRequested?.Invoke();
            }

            // Kategorie wählen und Zug beenden
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(scoreCard, cup.Dice);
            OnCategoryRequested?.Invoke(chosenCategory); 
        }

        private IEnumerator RunBotRoutine(DiceCup cup, ScoreCard scoreCard)
        {
            yield return new WaitForSeconds(UnityEngine.Random.Range(1.0f, 1.6f));

            for (int r = 0; r < 3; r++)
            {
                // A) WÜRFELN (Löst Event aus, statt _gameController direkt aufzurufen)
                OnRollRequested?.Invoke();

                // B) WARTEN auf Animation (Dauer wird nun von der UI gesteuert, Bot wartet nur stumpf)
                yield return new WaitForSeconds(1.7f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(1.0f, 1.4f));

                if (r == 2) break;

                // C) ENTSCHEIDEN
                List<int> diceToHold = BotLogic.GetDiceToHold(cup.Dice, scoreCard);

                if (diceToHold.Count == 5)
                {
                    foreach (int index in diceToHold)
                    {
                        if (!cup.Dice[index].IsHeld)
                        {
                            OnToggleHoldRequested?.Invoke(index);
                        }
                    }
                    break; 
                }

                // E) NORMALES AUSWÄHLEN
                for (int i = 0; i < 5; i++)
                {
                    bool shouldHold = diceToHold.Contains(i);
                    if (cup.Dice[i].IsHeld != shouldHold)
                    {
                        OnToggleHoldRequested?.Invoke(i);
                        yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.8f));
                    }
                }
                yield return new WaitForSeconds(1.2f);
            }

            yield return new WaitForSeconds(1.6f);

            // Bonus Check
            if (scoreCard.UpperSectionRaw >= 63 && !scoreCard.IsBonusClaimed)
            {
                yield return new WaitForSeconds(0.5f);
                OnBonusClaimRequested?.Invoke();
                yield return new WaitForSeconds(1.5f);
            }

            // Kategorie wählen
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(scoreCard, cup.Dice);
            OnCategoryRequested?.Invoke(chosenCategory); 
        }
    }
}