using System;
using System.Collections.Generic;
using System.Linq;
using DiceGame.Core.Models;
using DiceGame.Core.Rules;

namespace DiceGame.Core.AI
{
    public static class BotLogic
    {
        // 1. Entscheiden, welche Würfel gehalten werden sollen
        public static List<int> GetDiceToHold(List<Die> dice)
        {
            List<int> indicesToHold = new List<int>();

            var groups = dice.GroupBy(d => d.Value)
                             .OrderByDescending(g => g.Count())
                             .ToList();

            // Wenn wir einen Kniffel haben (5 gleiche), alle festhalten!
            if (groups[0].Count() == 5)
            {
                return new List<int> { 0, 1, 2, 3, 4 };
            }

            // Wenn wir Pärchen, Drillinge oder Vierlinge haben, behalte sie!
            if (groups[0].Count() >= 2)
            {
                int targetValue = groups[0].Key;
                for (int i = 0; i < dice.Count; i++)
                {
                    if (dice[i].Value == targetValue) indicesToHold.Add(i);
                }
                return indicesToHold;
            }

            // Fallback: Keine Pärchen? Behalte wenigstens die 5en und 6en
            for (int i = 0; i < dice.Count; i++)
            {
                if (dice[i].Value >= 5) indicesToHold.Add(i);
            }

            return indicesToHold;
        }

        // 2. Entscheiden, welche Kategorie am Ende angeklickt wird
        public static ScoreCategory ChooseBestCategory(ScoreCard scoreCard, List<Die> dice)
        {
            // --- PRIO 1: DIE FESTEN, HOHEN WERTE SICHERN ---
            
            if (!scoreCard.IsCategoryFilled(ScoreCategory.NicerDicer) && ScoreCalculator.CalculateScore(dice, ScoreCategory.NicerDicer) == 50)
                return ScoreCategory.NicerDicer;

            if (!scoreCard.IsCategoryFilled(ScoreCategory.LargeStraight) && ScoreCalculator.CalculateScore(dice, ScoreCategory.LargeStraight) == 40)
                return ScoreCategory.LargeStraight;

            if (!scoreCard.IsCategoryFilled(ScoreCategory.SmallStraight) && ScoreCalculator.CalculateScore(dice, ScoreCategory.SmallStraight) == 30)
                return ScoreCategory.SmallStraight;

            if (!scoreCard.IsCategoryFilled(ScoreCategory.FullHouse) && ScoreCalculator.CalculateScore(dice, ScoreCategory.FullHouse) == 25)
                return ScoreCategory.FullHouse;

            // --- PRIO 2: NORMALE PUNKTE (Mit leichtem Bonus für die obere Sektion) ---
            
            ScoreCategory bestCategory = ScoreCategory.Ones; // Fallback
            int maxScore = -1;

            foreach (ScoreCategory category in Enum.GetValues(typeof(ScoreCategory)))
            {
                if (!scoreCard.IsCategoryFilled(category))
                {
                    int score = ScoreCalculator.CalculateScore(dice, category);
                    int weight = 0;

                    // Wenn wir hier Punkte machen können, bevorzugen wir die oberen Felder für den 63er-Bonus
                    if (score > 0 && category >= ScoreCategory.Ones && category <= ScoreCategory.Sixes)
                    {
                        weight = 2; // Virtueller Bonus für die Entscheidung
                    }

                    if (score + weight > maxScore)
                    {
                        maxScore = score + weight;
                        bestCategory = category;
                    }
                }
            }

            // --- PRIO 3: SCHADENSBEGRENZUNG (Streichen) ---
            // Wenn der Bot absolut keinen einzigen Punkt machen kann (maxScore == 0)
            if (maxScore == 0)
            {
                // Streiche lieber die schweren Dinge weg als die wichtigen Zahlen
                if (!scoreCard.IsCategoryFilled(ScoreCategory.NicerDicer)) return ScoreCategory.NicerDicer;
                if (!scoreCard.IsCategoryFilled(ScoreCategory.LargeStraight)) return ScoreCategory.LargeStraight;
                if (!scoreCard.IsCategoryFilled(ScoreCategory.Ones)) return ScoreCategory.Ones; // 1er opfern ist oft okay
                if (!scoreCard.IsCategoryFilled(ScoreCategory.Twos)) return ScoreCategory.Twos; 
            }

            return bestCategory;
        }
    }
}