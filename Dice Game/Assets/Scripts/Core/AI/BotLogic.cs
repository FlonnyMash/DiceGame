using System;
using System.Collections.Generic;
using System.Linq;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;

namespace DiceGame.Core.AI
{
    public static class BotLogic
    {
        // Fix für CS7036: Diese Methode braucht jetzt zwingend die scoreCard
        public static bool WantsToRollAgain(List<Die> dice, ScoreCard scoreCard)
        {
            List<int> heldIndices = GetDiceToHold(dice, scoreCard);
            if (heldIndices.Count == 5) return false;
            return true;
        }

        public static List<int> GetDiceToHold(List<Die> dice, ScoreCard scoreCard)
        {
            List<int> indicesToHold = new List<int>();

            // 1. ANALYSE DER WÜRFEL (Gruppieren für Pasche / Full House)
            var groups = dice.GroupBy(d => d.Value)
                            .OrderByDescending(g => g.Count())
                            .ThenByDescending(g => g.Key)
                            .ToList();

            int bestValue = groups[0].Key;
            int count = groups[0].Count();

            // --- PRIO 1: KNIFFEL ---
            if (count == 5 && !scoreCard.IsCategoryFilled(ScoreCategory.NicerDicer))
            {
                return new List<int> { 0, 1, 2, 3, 4 };
            }

            // --- PRIO 2: FULL HOUSE ---
            if (groups.Count == 2 && groups[0].Count() == 3 && groups[1].Count() == 2)
            {
                if (!scoreCard.IsCategoryFilled(ScoreCategory.FullHouse))
                {
                    return new List<int> { 0, 1, 2, 3, 4 };
                }
            }

            // --- PRIO 3: VIERERPASCH SCHÜTZEN ---
            // Bevor er auf Straßenjagd geht: Wenn wir 4 gleiche haben, niemals aufgeben!
            if (count == 4 && (!scoreCard.IsCategoryFilled(ScoreCategory.FourOfAKind) || !scoreCard.IsCategoryFilled(ScoreCategory.NicerDicer) || !scoreCard.IsCategoryFilled((ScoreCategory)(bestValue - 1))))
            {
                for (int i = 0; i < dice.Count; i++)
                    if (dice[i].Value == bestValue) indicesToHold.Add(i);
                return indicesToHold;
            }

            // --- STRASSEN-VORBEREITUNG ---
            var uniqueDice = dice.Select((d, index) => new { d.Value, index })
                                .GroupBy(x => x.Value)
                                .Select(g => g.First())
                                .OrderBy(x => x.Value)
                                .ToList();

            bool largeStraightOpen = !scoreCard.IsCategoryFilled(ScoreCategory.LargeStraight);
            bool smallStraightOpen = !scoreCard.IsCategoryFilled(ScoreCategory.SmallStraight);
            
            // Wir deklarieren die Liste hier, damit wir sie später bei Prio 6 noch nutzen können
            List<int> bestTargetIndices = new List<int>();

            // --- PRIO 4: GUTE STRASSEN (4+ Würfel) ---
            if (largeStraightOpen || smallStraightOpen)
            {
                // Wir prüfen zwei mögliche Ziel-Straßen: 1-2-3-4-5 und 2-3-4-5-6
                List<int> target1 = new List<int> { 1, 2, 3, 4, 5 };
                List<int> target2 = new List<int> { 2, 3, 4, 5, 6 };

                List<int> indicesForTarget1 = new List<int>();
                List<int> indicesForTarget2 = new List<int>();

                for (int i = 0; i < dice.Count; i++)
                {
                    // Wenn der Würfel in Ziel 1 passt und wir diesen Wert noch nicht markiert haben
                    if (target1.Contains(dice[i].Value) && !indicesForTarget1.Any(idx => dice[idx].Value == dice[i].Value))
                        indicesForTarget1.Add(i);

                    // Das gleiche für Ziel 2
                    if (target2.Contains(dice[i].Value) && !indicesForTarget2.Any(idx => dice[idx].Value == dice[i].Value))
                        indicesForTarget2.Add(i);
                }

                // Wir entscheiden uns für das Ziel, bei dem wir schon mehr Würfel haben
                bestTargetIndices = indicesForTarget1.Count >= indicesForTarget2.Count 
                                            ? indicesForTarget1 
                                            : indicesForTarget2;

                // ÄNDERUNG: Wir behalten hier NUR fertige oder fast fertige Straßen (4 oder 5 Würfel)
                if (bestTargetIndices.Count >= 4)
                {
                    return bestTargetIndices;
                }
            }

            // --- PRIO 5: PAARE & DREIERPASCH FÜR BONUS ODER FULL HOUSE ---
            ScoreCategory upperCat = (ScoreCategory)(bestValue - 1);
            bool needsUpper = !scoreCard.IsCategoryFilled(upperCat);
            bool needsFullHouse = !scoreCard.IsCategoryFilled(ScoreCategory.FullHouse);

            // Wenn wir 3 gleiche haben und Full House oder oben noch offen ist -> Behalte die 3!
            if (count == 3 && (needsFullHouse || needsUpper || !scoreCard.IsCategoryFilled(ScoreCategory.ThreeOfAKind)))
            {
                for (int i = 0; i < dice.Count; i++)
                    if (dice[i].Value == bestValue) indicesToHold.Add(i);
                return indicesToHold;
            }

            // Wenn wir ein Paar haben und das Feld oben noch offen ist -> Behalten!
            if (count == 2 && needsUpper)
            {
                for (int i = 0; i < dice.Count; i++)
                    if (dice[i].Value == bestValue) indicesToHold.Add(i);
                return indicesToHold;
            }

            // --- PRIO 6: HALBE STRASSEN (3 Würfel) ---
            // Wenn wir bis hierhin gekommen sind, gab es keine guten Paare.
            // Jetzt ist es schlau, auf die 3er-Straße zurückzugreifen!
            if (bestTargetIndices.Count == 3)
            {
                return bestTargetIndices;
            }

            // --- PRIO 7: FALLBACK (Der "Alles Rerollen"-Check) ---
            // Wenn wir keine Paare, keine Straßen und keine Kniffel in Sicht haben:
            
            bool chanceOpen = !scoreCard.IsCategoryFilled(ScoreCategory.Chance);

            for (int i = 0; i < dice.Count; i++)
            {
                int val = dice[i].Value;
                ScoreCategory upperCatForVal = (ScoreCategory)(val - 1);
                
                // Wir behalten eine hohe Zahl (5 oder 6) NUR dann, wenn:
                // 1. Das dazugehörige Feld oben noch offen ist ODER
                // 2. Die Chance noch offen ist (dann sammeln wir einfach Punkte)
                if (val >= 5 && (!scoreCard.IsCategoryFilled(upperCatForVal) || chanceOpen))
                {
                    indicesToHold.Add(i);
                }
            }

            // Wenn indicesToHold an dieser Stelle LEER ist (0 Elemente), 
            // gibt die Methode eine leere Liste zurück. 
            // Das signalisiert deinem BotController automatisch: "Behalte NICHTS, würfle alles neu!"
            return indicesToHold;
        }

        public static ScoreCategory ChooseBestCategory(ScoreCard scoreCard, List<Die> dice)
        {
            ScoreCategory bestCategory = ScoreCategory.Ones;
            float maxWeight = -1000f;

            foreach (ScoreCategory cat in Enum.GetValues(typeof(ScoreCategory)))
            {
                if (scoreCard.IsCategoryFilled(cat)) continue;

                int actualScore = ScoreCalculator.CalculateScore(dice, cat);
                float currentWeight = actualScore;

                // --- STRATEGIE-GEWICHTUNG ---

                // A) Bonus-Gier & Pace (Ziel-System): Felder oben (1-6)
                if (cat <= ScoreCategory.Sixes)
                {
                    int faceValue = (int)cat + 1; 
                    int targetScore = faceValue * 3; // Das Soll-Ziel (z.B. 3 * 6 = 18 Punkte)

                    if (actualScore >= targetScore)
                    {
                        // Ziel erreicht oder übertroffen (3 oder mehr Würfel)!
                        // Gibt Extra-Gewicht für das Polster UND den fetten +20 Bonus von deiner alten Logik
                        currentWeight += (actualScore - targetScore) * 1.5f; 
                        currentWeight += 20; 
                    }
                    else
                    {
                        // Ziel verfehlt! Wir hinken dem Bonus hinterher.
                        int deficit = targetScore - actualScore;

                        if (faceValue >= 4)
                        {
                            // 4er, 5er und 6er sind HEILIG! Harte Strafe, wenn wir hier zu wenig eintragen (z.B. nur zwei 6er).
                            currentWeight -= deficit * 4f; 
                        }
                        else
                        {
                            // Bei 1ern, 2ern und 3ern ist das Opfern oder Untererfüllen okay.
                            currentWeight -= deficit * 1f;
                        }
                    }
                }

                // B) Chance-Schutz: Benutze die Chance niemals für wenig Punkte!
                if (cat == ScoreCategory.Chance)
                {
                    if (actualScore < 20) currentWeight -= 50; 
                    else currentWeight += 5; 
                }

                // --- NEU: PASCH-OPPORTUNISMUS ---
                // Ein hoher Viererpasch ist selten und sollte genutzt werden, wenn er sich anbietet!
                if (cat == ScoreCategory.FourOfAKind)
                {
                    // Ein 4er-Pasch ab 22 Punkten (z.B. vier 5er + 2, oder vier 6er) ist fantastisch.
                    if (actualScore >= 22) currentWeight += 25f; // Extra-Gewicht, um die Bonus-Gier auszustechen!
                }
                // Auch ein extrem guter Dreierpasch (z.B. drei 6er + 5 + 4 = 27 Punkte) ist oft unten besser aufgehoben
                else if (cat == ScoreCategory.ThreeOfAKind)
                {
                    if (actualScore >= 24) currentWeight += 15f;
                }

                // C) Opfer-Logik (Wenn fast nichts gewürfelt wurde)
                if (actualScore == 0)
                {
                    // Wenn wir streichen MÜSSEN:
                    // 1er und 2er zu streichen ist okay (-10 Strafe)
                    // Kniffel oder Straßen zu streichen ist fatal (-100 Strafe)
                    if (cat == ScoreCategory.Ones) currentWeight = -10;
                    else if (cat == ScoreCategory.Twos) currentWeight = -15;
                    else if (cat == ScoreCategory.NicerDicer) currentWeight = -200;
                    else currentWeight -= 50;
                }
                else if (actualScore < 5 && (cat == ScoreCategory.Ones || cat == ScoreCategory.Twos))
                {
                    // Wenn wir nur einen 1er haben, ist es oft besser, den da einzutragen
                    // als eine Chance mit 7 Punkten zu verschwenden.
                    currentWeight += 15; 
                }

                if (currentWeight > maxWeight)
                {
                    maxWeight = currentWeight;
                    bestCategory = cat;
                }
            }

            return bestCategory;
        }
    }
}