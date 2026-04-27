using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Core.AI;
using DiceGame.Core.Rules;

namespace DiceGame.Controllers
{
    public class BotController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameController _gameController;

        private void Start()
        {
            if (_gameController != null) _gameController.OnTurnStarted += HandleTurnStarted;
        }

        private void OnDestroy()
        {
            if (_gameController != null) _gameController.OnTurnStarted -= HandleTurnStarted;
        }

        private void HandleTurnStarted()
        {
            if (_gameController.CurrentPlayer.Name == "Bot")
            {
                StartCoroutine(RunBotRoutine());
            }
        }

        private IEnumerator RunBotRoutine()
        {
            // 1. Schnelle Beobachtung am Anfang
            yield return new WaitForSeconds(Random.Range(1.0f, 1.6f));

            for (int r = 0; r < 3; r++)
            {
                // A) WÜRFELN 
                // Wichtig: Wir lassen NICHTS los. Er würfelt nur die freien Würfel.
                _gameController.OnRollButtonClicked();

                // B) WARTEN auf Animation (Dauer 1.5s)
                // Wir warten 1.7s, damit wir das Ergebnis kurz sehen können
                yield return new WaitForSeconds(1.7f);

                // Die Würfel liegen jetzt still. Der Bot "überlegt" nun kurz, was er sieht.
                yield return new WaitForSeconds(Random.Range(1.0f, 1.4f));

                // Wenn es der letzte Wurf war, müssen wir nichts mehr auswählen
                if (r == 2) break;

                // C) ENTSCHEIDEN & NEU HIGHLIGHTEN
                // Jetzt schaut der Bot: "Was hab ich da liegen?"
                // Wir geben jetzt auch die ScoreCard des aktuellen Spielers mit!
                List<int> diceToHold = BotLogic.GetDiceToHold(
                    _gameController.DiceCup.Dice, 
                    _gameController.CurrentPlayer.ScoreCard
                );

                // D) AMNESIE / RESET (Nur für die neue Entscheidung)
                // Wir setzen erst JETZT den Status neu, basierend auf der Logik
                for (int i = 0; i < _gameController.DiceCup.Dice.Count; i++)
                {
                    bool shouldHold = diceToHold.Contains(i);
                    
                    // Nur wenn sich der Status ändert, machen wir eine kleine Pause
                    if (_gameController.DiceCup.Dice[i].IsHeld != shouldHold)
                    {
                        _gameController.DiceCup.Dice[i].IsHeld = shouldHold;
                        _gameController.DieViews[i].UpdateView(_gameController.DiceCup.Dice[i].Value, shouldHold);
                        
                        // Schnelleres Markieren (0.5s - 0.9s)
                        yield return new WaitForSeconds(Random.Range(0.5f, 0.9f));
                    }
                }

                // E) SINNLOS-CHECK: Wenn alle 5 gehalten werden, direkt Schluss

                // --- DER FIX FÜR INSTANT-LOCK ---
                if (diceToHold.Count == 5)
                {
                    // Alle 5 Würfel SOFORT markieren ohne Pause
                    foreach (int index in diceToHold)
                    {
                        if (!_gameController.DiceCup.Dice[index].IsHeld)
                        {
                            _gameController.DiceCup.Dice[index].IsHeld = true;
                            _gameController.DieViews[index].UpdateView(_gameController.DiceCup.Dice[index].Value, true);
                        }
                    }
                    
                    // Schleife direkt abbrechen, er ist fertig!
                    break; 
                }

                // Wenn es NICHT 5 sind, macht er es ganz normal mit menschlichen Pausen
                for (int i = 0; i < 5; i++)
                {
                    bool shouldHold = diceToHold.Contains(i);
                    
                    if (_gameController.DiceCup.Dice[i].IsHeld != shouldHold)
                    {
                        _gameController.DiceCup.Dice[i].IsHeld = shouldHold;
                        _gameController.DieViews[i].UpdateView(_gameController.DiceCup.Dice[i].Value, shouldHold);
                        
                        // Normale Denkpause zwischen einzelnen Klicks
                        yield return new WaitForSeconds(0.3f);
                    }
                }

                // F) Pause vor dem nächsten Wurf (Bot "atmet" kurz durch)
                yield return new WaitForSeconds(1.2f);
            }

            // 2. Finale Denkpause vor der Punktevergabe (etwas flotter)
            yield return new WaitForSeconds(1.6f);

            // 3. Kategorie wählen
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(
                _gameController.CurrentPlayer.ScoreCard, 
                _gameController.DiceCup.Dice
            );
            
            _gameController.HandleCategoryClicked(chosenCategory); 
        }
    }
}