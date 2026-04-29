using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Rules;
using DiceGame.Core.Models;

namespace DiceGame.UI.Views
{
    public class ScoreCardView : MonoBehaviour
    {
        [Header("Containers (Left/Right Layout)")]
        [SerializeField] private Transform _leftBlockContainer;  // Für 1er bis 6er
        [SerializeField] private Transform _rightBlockContainer; // Für Pasche, Full House etc.

        [Header("Prefabs")]
        [SerializeField] private ScoreRowView _rowPrefab;
        [SerializeField] private BonusRowView _bonusRowPrefab;
        
        [Header("Totals")]
        [SerializeField] private TextMeshProUGUI _upperBonusText;
        [SerializeField] private TextMeshProUGUI _grandTotalText;

        // Ein Dictionary, um schnell die richtige Zeile zu finden
        private Dictionary<ScoreCategory, ScoreRowView> _rows = new Dictionary<ScoreCategory, ScoreRowView>();

        // Gibt die Events der einzelnen Zeilen nach oben an den Controller weiter
        public event System.Action<ScoreCategory> OnCategoryClicked;

        private BonusRowView _bonusRowInstance;

        public void Initialize()
        {
            // Alte Buttons aufräumen, falls das UI neu geladen wird
            foreach (Transform child in _leftBlockContainer) Destroy(child.gameObject);
            foreach (Transform child in _rightBlockContainer) Destroy(child.gameObject);
            _rows.Clear();

            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                // Bestimme, ob der Button links oder rechts erscheinen soll
                Transform targetContainer = IsLeftColumn(category) ? _leftBlockContainer : _rightBlockContainer;

                ScoreRowView newRow = Instantiate(_rowPrefab, targetContainer, false);
                
                // Macht aus "ThreeOfAKind" -> "Three Of A Kind"
                string displayName = System.Text.RegularExpressions.Regex.Replace(category.ToString(), "([a-z])([A-Z])", "$1 $2");
                
                newRow.Initialize(category, displayName);
                newRow.OnRowClicked += (cat) => OnCategoryClicked?.Invoke(cat);
                _rows.Add(category, newRow);
            }

            if (_bonusRowPrefab != null)
            {
                _bonusRowInstance = Instantiate(_bonusRowPrefab, _leftBlockContainer, false);
                // Durch SetAsLastSibling stellen wir sicher, dass es GANZ UNTEN in der LeftColumn landet
                _bonusRowInstance.transform.SetAsLastSibling(); 
                _bonusRowInstance.Initialize(63);
            }

            UpdateTotals(0, 0, 0);
        }

        // Trennungs-Logik: 1er bis 6er kommen nach links, der Rest nach rechts
        private bool IsLeftColumn(ScoreCategory category)
        {
            // In deinem Enum sind Ones bis Sixes der linke/obere Block
            return (int)category <= (int)ScoreCategory.Sixes;
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
            // Gibt die erreichten Punkte (ohne den Bonus selbst) an den Balken weiter
            if (_bonusRowInstance != null)
            {
                _bonusRowInstance.UpdateBonusProgress(upperRaw);
            }
            
            if (_grandTotalText != null)
                _grandTotalText.text = $"Total: {grandTotal}";
        }

        public void RefreshDisplay(ScoreCard scoreCard)
        {
            foreach (var kvp in _rows)
            {
                int? score = scoreCard.GetScore(kvp.Key);
                if (score.HasValue)
                {
                    kvp.Value.SetFinalScore(score.Value);
                }
                else
                {
                    kvp.Value.Clear();
                }
            }
            UpdateTotals(scoreCard.UpperSectionRaw, scoreCard.UpperSectionBonus, scoreCard.GrandTotal);
        }
    }
}