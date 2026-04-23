using System;
using System.Collections.Generic;
using System.Linq;
using DiceGame.Core.Rules;

namespace DiceGame.Core.Models
{
    public class ScoreCard
    {
        // Speichert die Punkte pro Kategorie. 'null' bedeutet: Noch nicht bespielt.
        private readonly Dictionary<ScoreCategory, int?> _scores;

        public event Action OnScoreCardChanged;

        public ScoreCard()
        {
            _scores = new Dictionary<ScoreCategory, int?>();
            
            // Initialisiere alle Kategorien als leer
            foreach (ScoreCategory category in Enum.GetValues(typeof(ScoreCategory)))
            {
                _scores[category] = null;
            }
        }

        public bool IsCategoryFilled(ScoreCategory category) => _scores[category].HasValue;

        public int? GetScore(ScoreCategory category) => _scores[category];

        public bool SetScore(ScoreCategory category, int score)
        {
            if (IsCategoryFilled(category)) return false; // Bereits belegt!

            _scores[category] = score;
            OnScoreCardChanged?.Invoke();
            return true;
        }

        // --- BERECHNUNGEN ---

        public int UpperSectionRaw => GetCategoriesSum(new[] {
            ScoreCategory.Ones, ScoreCategory.Twos, ScoreCategory.Threes,
            ScoreCategory.Fours, ScoreCategory.Fives, ScoreCategory.Sixes
        });

        public int UpperSectionBonus => UpperSectionRaw >= 63 ? 35 : 0;

        public int LowerSectionTotal => GetCategoriesSum(new[] {
            ScoreCategory.ThreeOfAKind, ScoreCategory.FourOfAKind, ScoreCategory.FullHouse,
            ScoreCategory.SmallStraight, ScoreCategory.LargeStraight, ScoreCategory.Yahtzee,
            ScoreCategory.Chance
        });

        public int GrandTotal => UpperSectionRaw + UpperSectionBonus + LowerSectionTotal;

        private int GetCategoriesSum(IEnumerable<ScoreCategory> categories)
        {
            return categories
                .Select(c => _scores[c] ?? 0) // Wenn null, dann 0 für die Summe
                .Sum();
        }
    }
}