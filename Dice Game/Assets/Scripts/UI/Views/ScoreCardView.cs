using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Rules;

namespace DiceGame.UI.Views
{
    public class ScoreCardView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _rowsContainer; // Hier spawnen wir die Prefabs
        [SerializeField] private ScoreRowView _rowPrefab;
        
        [Header("Totals")]
        [SerializeField] private TextMeshProUGUI _upperBonusText;
        [SerializeField] private TextMeshProUGUI _grandTotalText;

        // Ein Dictionary, um schnell die richtige Zeile zu finden
        private Dictionary<ScoreCategory, ScoreRowView> _rows = new Dictionary<ScoreCategory, ScoreRowView>();

        // Gibt die Events der einzelnen Zeilen nach oben an den Controller weiter
        public event System.Action<ScoreCategory> OnCategoryClicked;

        public void Initialize()
        {
            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                ScoreRowView newRow = Instantiate(_rowPrefab, _rowsContainer, false);
                
                // Macht aus "ThreeOfAKind" -> "Three Of A Kind"
                string displayName = System.Text.RegularExpressions.Regex.Replace(category.ToString(), "([a-z])([A-Z])", "$1 $2");
                
                newRow.Initialize(category, displayName);
                newRow.OnRowClicked += (cat) => OnCategoryClicked?.Invoke(cat);
                _rows.Add(category, newRow);
            }
            UpdateTotals(0, 0, 0);
        }

        public void ShowPotentialScore(ScoreCategory category, int points)
        {
            _rows[category].ShowPotentialScore(points);
        }

        public void SetFinalScore(ScoreCategory category, int points)
        {
            _rows[category].SetFinalScore(points);
        }

        public void ClearAllPotentials()
        {
            foreach (var row in _rows.Values)
            {
                // Wenn der Text grau ist (also noch nicht final eingetragen), löschen wir ihn
                if (row.GetComponentInChildren<Button>().interactable) 
                {
                    row.Clear();
                }
            }
        }

        public void UpdateTotals(int upperRaw, int upperBonus, int grandTotal)
        {
            _upperBonusText.text = $"Bonus ({upperRaw}/63): {upperBonus}";
            _grandTotalText.text = $"Total: {grandTotal}";
        }
    }
}