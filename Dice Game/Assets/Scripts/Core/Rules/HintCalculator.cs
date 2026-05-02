using System.Collections.Generic;
using DiceGame.Core.Models;

namespace DiceGame.Core.Rules
{
    public static class HintCalculator
    {
        /// <summary>
        /// Bewertet die aktuellen Würfel und gibt die stärkste mögliche Kategorie als Hinweis zurück.
        /// Gibt null zurück, wenn nichts Gutes verfügbar ist.
        /// </summary>
        public static ScoreCategory? GetBestHint(ScoreCard scoreCard, List<Die> dice, int rollsLeft)
        {
            ScoreCategory? bestCategory = null;
            int maxScore = 0;

            // 1. Wir prüfen nur die starken Kombinationen (Unterer Block exklusive Chance)
            ScoreCategory[] strongCategories = {
                ScoreCategory.NicerDicer,     // Prio 1
                ScoreCategory.LargeStraight, 
                ScoreCategory.SmallStraight, 
                ScoreCategory.FullHouse, 
                ScoreCategory.FourOfAKind, 
                ScoreCategory.ThreeOfAKind
            };

            foreach (var cat in strongCategories)
            {
                if (!scoreCard.IsCategoryFilled(cat))
                {
                    int score = ScoreCalculator.CalculateScore(dice, cat);
                    // Wenn die Kombination gültig ist (> 0 Punkte) und stärker als die bisherige
                    if (score > 0 && score > maxScore)
                    {
                        maxScore = score;
                        bestCategory = cat;
                    }
                }
            }

            // 2. Spezialregel für die "Chance"
            // Nur wenn keine andere starke Combo gefunden wurde, keine Würfe mehr übrig sind
            // und das Chance-Feld noch frei ist.
            if (bestCategory == null && rollsLeft == 0 && !scoreCard.IsCategoryFilled(ScoreCategory.Chance))
            {
                int chanceScore = ScoreCalculator.CalculateScore(dice, ScoreCategory.Chance);
                
                // Nur aufploppen, wenn es ein "guter" Wurf ist (z. B. 20 Punkte oder mehr)
                if (chanceScore >= 20)
                {
                    bestCategory = ScoreCategory.Chance;
                }
            }

            // 1er bis 6er werden bewusst komplett ignoriert, wie von dir gewünscht.
            return bestCategory;
        }
    }
}