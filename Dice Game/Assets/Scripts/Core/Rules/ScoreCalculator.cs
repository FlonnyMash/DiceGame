using System.Collections.Generic;
using System.Linq;
using DiceGame.Core.Models;

namespace DiceGame.Core.Rules
{
    public static class ScoreCalculator
    {
        public static int CalculateScore(List<Die> dice, ScoreCategory category)
        {
            List<int> values = dice.Select(d => d.Value).OrderBy(v => v).ToList();

            switch (category)
            {
                // Oberer Block
                case ScoreCategory.Ones:   return SumOfSpecificValue(values, 1);
                case ScoreCategory.Twos:   return SumOfSpecificValue(values, 2);
                case ScoreCategory.Threes: return SumOfSpecificValue(values, 3);
                case ScoreCategory.Fours:  return SumOfSpecificValue(values, 4);
                case ScoreCategory.Fives:  return SumOfSpecificValue(values, 5);
                case ScoreCategory.Sixes:  return SumOfSpecificValue(values, 6);

                // Unterer Block
                case ScoreCategory.ThreeOfAKind:    return HasNOfAKind(values, 3) ? values.Sum() : 0;
                case ScoreCategory.FourOfAKind:     return HasNOfAKind(values, 4) ? values.Sum() : 0;
                case ScoreCategory.FullHouse:       return IsFullHouse(values) ? 25 : 0;
                case ScoreCategory.SmallStraight:   return IsStraight(values, 4) ? 30 : 0;
                case ScoreCategory.LargeStraight:   return IsStraight(values, 5) ? 40 : 0;
                case ScoreCategory.NicerDicer:      return HasNOfAKind(values, 5) ? 50 : 0;
                case ScoreCategory.Chance:          return values.Sum();
                
                default: return 0;
            }
        }

        private static int SumOfSpecificValue(List<int> values, int target) => values.Where(v => v == target).Sum();
        
        private static bool HasNOfAKind(List<int> values, int n) => 
            values.GroupBy(v => v).Any(g => g.Count() >= n);

        private static bool IsFullHouse(List<int> values) => 
            values.GroupBy(v => v).Count() == 2 && (values.GroupBy(v => v).Any(g => g.Count() == 3));

        private static bool IsStraight(List<int> values, int length)
        {
            var distinct = values.Distinct().ToArray();
            if (distinct.Length < length) return false;
            
            int count = 1;
            int maxCount = 1;
            for (int i = 0; i < distinct.Length - 1; i++)
            {
                if (distinct[i + 1] == distinct[i] + 1) count++;
                else count = 1;
                maxCount = System.Math.Max(maxCount, count);
            }
            return maxCount >= length;
        }
    }
}