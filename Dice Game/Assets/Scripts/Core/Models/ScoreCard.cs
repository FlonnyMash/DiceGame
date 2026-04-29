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

        // NEU: Speichert, ob der Bonus abgeholt wurde
        public bool IsBonusClaimed { get; private set; }

        public ScoreCard()
        {
            _scores = new Dictionary<ScoreCategory, int?>();
            IsBonusClaimed = false;
            
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

            // SICHERHEITS-NETZ: Wenn das die letzte Eingabe des Spiels war 
            // und der Bonus erreicht, aber vergessen wurde, wird er jetzt automatisch geclaimt.
            if (IsComplete && IsBonusEligible && !IsBonusClaimed)
            {
                IsBonusClaimed = true;
            }

            OnScoreCardChanged?.Invoke();
            return true;
        }

        // --- NEU: CLAIM LOGIK ---

        // Prüft, ob der Spieler die 63 Punkte überhaupt voll hat
        public bool IsBonusEligible => UpperSectionRaw >= 63;

        // Wird vom Controller aufgerufen, wenn der Spieler auf den hüpfenden Button drückt
        public void ClaimBonus()
        {
            if (IsBonusEligible && !IsBonusClaimed)
            {
                IsBonusClaimed = true;
                OnScoreCardChanged?.Invoke(); // UI benachrichtigen, dass sich das Total geändert hat
            }
        }

        // --- BERECHNUNGEN ---

        public int UpperSectionRaw => GetCategoriesSum(new[] {
            ScoreCategory.Ones, ScoreCategory.Twos, ScoreCategory.Threes,
            ScoreCategory.Fours, ScoreCategory.Fives, ScoreCategory.Sixes
        });

        // GEÄNDERT: Gibt die 35 Punkte nur zurück, wenn sie auch eingesammelt wurden
        public int UpperSectionBonus => IsBonusClaimed ? 35 : 0;

        public int LowerSectionTotal => GetCategoriesSum(new[] {
            ScoreCategory.ThreeOfAKind, ScoreCategory.FourOfAKind, ScoreCategory.FullHouse,
            ScoreCategory.SmallStraight, ScoreCategory.LargeStraight, ScoreCategory.NicerDicer,
            ScoreCategory.Chance
        });

        public int GrandTotal => UpperSectionRaw + UpperSectionBonus + LowerSectionTotal;

        private int GetCategoriesSum(IEnumerable<ScoreCategory> categories)
        {
            return categories
                .Select(c => _scores[c] ?? 0) // Wenn null, dann 0 für die Summe
                .Sum();
        }

        public bool IsComplete => _scores.Values.All(score => score.HasValue);

        public int FilledCategoriesCount => _scores.Values.Count(score => score.HasValue);
    }
}