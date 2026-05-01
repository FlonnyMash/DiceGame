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

        [Header("Animation")]
        [SerializeField] private Animator _skipBotAnimator; // NEU: Animator für den Skip-Button

        private Coroutine _botCoroutine; 
        private Coroutine _skipFinaleCoroutine; // NEU: Für das Finale nach dem Skip
        private bool _isSkipping = false;

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
                _isSkipping = false;
                _botCoroutine = StartCoroutine(RunBotRoutine());
            }
        }

        // --- DIE SKIP-METHODE ---
        public void SkipBotTurn()
        {
            if (_isSkipping || _gameController.CurrentPlayer.Name != "Bot") return;
            _isSkipping = true;

            _skipBotAnimator.SetTrigger("OnPressed"); // Trigger für die Skip-Animation

            // 1. Die langsame, visuelle Haupt-Coroutine sofort abbrechen
            if (_botCoroutine != null) StopCoroutine(_botCoroutine);

            // 2. Das Würfeln im reinen Datenmodell sofort beenden
            FastForwardRolls();

            // 3. Kurze Pause, damit der Spieler die finalen Würfel sieht, bevor die Punkte eingetragen werden
            _skipFinaleCoroutine = StartCoroutine(FinishTurnAfterSkipRoutine());
        }

        private void FastForwardRolls()
        {
            // Solange der Bot noch würfeln darf...
            int emergencyBreak = 0; 
            while (_gameController.DiceCup.RollsLeft > 0 && emergencyBreak < 3)
            {
                emergencyBreak++;

                List<int> diceToHold = BotLogic.GetDiceToHold(
                    _gameController.DiceCup.Dice, 
                    _gameController.CurrentPlayer.ScoreCard
                );

                if (diceToHold.Count == 5) break; // Perfekter Wurf, aufhören!

                // Würfel im Datenmodell auf "Gehalten" setzen
                foreach (int index in diceToHold)
                {
                    _gameController.DiceCup.Dice[index].IsHeld = true;
                }

                // Den Core-DiceCup direkt würfeln lassen (ohne UI-Trigger!)
                _gameController.DiceCup.Roll(); 
            }

            // WICHTIG: Da wir die Animationen abgebrochen haben, müssen wir das UI 
            // jetzt hart auf die finalen Würfelwerte synchronisieren.
            for (int i = 0; i < _gameController.DiceCup.Dice.Count; i++)
            {
                var die = _gameController.DiceCup.Dice[i];
                // Wir schalten "IsHeld" hier für die Anzeige auf false, da der Wurf ja beendet ist 
                // und die Würfel final auf dem Tisch liegen.
                _gameController.DieViews[i].UpdateView(die.Value, false);
            }
        }

        private IEnumerator FinishTurnAfterSkipRoutine()
        {
            // Dem menschlichen Spieler 1.5 Sekunden geben, um die finalen 5 Würfel zu sehen
            yield return new WaitForSeconds(1.5f);

            // Bonus-Check
            var botScoreCard = _gameController.CurrentPlayer.ScoreCard;
            if (botScoreCard.UpperSectionRaw >= 63 && !botScoreCard.IsBonusClaimed)
            {
                _gameController.HandleBonusClaimed(); 
                yield return new WaitForSeconds(1.5f); // Wenn geclaimed wurde, kurz die Animation wirken lassen
            }

            // Kategorie wählen und den Zug regulär beenden
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(
                _gameController.CurrentPlayer.ScoreCard, 
                _gameController.DiceCup.Dice
            );
            
            _gameController.HandleCategoryClicked(chosenCategory); 
        }

        private IEnumerator RunBotRoutine()
        {
            // 1. Schnelle Beobachtung am Anfang
            yield return new WaitForSeconds(Random.Range(1.0f, 1.6f));

            for (int r = 0; r < 3; r++)
            {
                // A) WÜRFELN 
                _gameController.OnRollButtonClicked();

                // B) WARTEN auf Animation (Dauer 1.5s + Puffer)
                yield return new WaitForSeconds(1.7f);

                // Die Würfel liegen jetzt still. Der Bot "überlegt" nun kurz.
                yield return new WaitForSeconds(Random.Range(1.0f, 1.4f));

                // Wenn es der letzte Wurf war, müssen wir nichts mehr auswählen
                if (r == 2) break;

                // C) ENTSCHEIDEN & NEU HIGHLIGHTEN
                List<int> diceToHold = BotLogic.GetDiceToHold(
                    _gameController.DiceCup.Dice, 
                    _gameController.CurrentPlayer.ScoreCard
                );

                // D) SINNLOS-CHECK: Wenn alle 5 gehalten werden, direkt Schluss (Instant-Lock)
                if (diceToHold.Count == 5)
                {
                    foreach (int index in diceToHold)
                    {
                        if (!_gameController.DiceCup.Dice[index].IsHeld)
                        {
                            // --- NEU: Wir nutzen die zentrale Methode, das triggert die Animation! ---
                            _gameController.ToggleDieState(index);
                        }
                    }
                    
                    // Schleife direkt abbrechen, er ist fertig!
                    break; 
                }

                // E) NORMALES AUSWÄHLEN (mit menschlichen Pausen)
                for (int i = 0; i < 5; i++)
                {
                    bool shouldHold = diceToHold.Contains(i);
                    
                    // Nur wenn sich der Status ändern soll, klicken wir virtuell auf den Würfel
                    if (_gameController.DiceCup.Dice[i].IsHeld != shouldHold)
                    {
                        // --- NEU: Zentrale Methode aufrufen ---
                        _gameController.ToggleDieState(i);
                        
                        // Normale Denkpause zwischen einzelnen Klicks (inkl. Animation ansehen)
                        yield return new WaitForSeconds(Random.Range(0.4f, 0.8f));
                    }
                }

                // F) Pause vor dem nächsten Wurf (Bot "atmet" kurz durch)
                yield return new WaitForSeconds(1.2f);
            }

            // 2. Finale Denkpause vor der Punktevergabe
            yield return new WaitForSeconds(1.6f);

                        // --- NEU: BOT HOLT SICH DEN BONUS ---
            var botScoreCard = _gameController.CurrentPlayer.ScoreCard;

            // Wenn er 63 oder mehr Punkte hat, den Bonus aber noch nicht abgeholt hat:
            if (botScoreCard.UpperSectionRaw >= 63 && !botScoreCard.IsBonusClaimed)
            {
                // Kurze Denkpause (Bot realisiert: "Oh, ich hab den Bonus!")
                yield return new WaitForSeconds(0.5f);
                
                // Bot drückt virtuell auf den Claim-Button
                // WICHTIG: Stelle sicher, dass diese Methode in deinem GameController "public" ist!
                _gameController.HandleBonusClaimed(); 
                
                // Warte, bis die coole Claim-Animation abgespielt ist, bevor der Zug weitergeht
                yield return new WaitForSeconds(1.5f);
            }
            // ------------------------------------

            // 3. Kategorie wählen
            ScoreCategory chosenCategory = BotLogic.ChooseBestCategory(
                _gameController.CurrentPlayer.ScoreCard, 
                _gameController.DiceCup.Dice
            );
            
            _gameController.HandleCategoryClicked(chosenCategory); 
        }
    }
}