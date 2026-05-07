using System.Collections.Generic;
using System.IO;
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

        public event System.Action OnBonusClaimClicked;

        private BonusRowView _bonusRowInstance;

        // #region agent log
        private static void AgentLog(string runId, string hypothesisId, string location, string message, string dataJson)
        {
            try
            {
                File.AppendAllText("debug-f7e117.log", $"{{\"sessionId\":\"f7e117\",\"runId\":\"{runId}\",\"hypothesisId\":\"{hypothesisId}\",\"location\":\"{location}\",\"message\":\"{message}\",\"data\":{dataJson},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n");
            }
            catch { }
        }
        // #endregion

        public void Initialize()
        {
            foreach (Transform child in _leftBlockContainer) Destroy(child.gameObject);
            foreach (Transform child in _rightBlockContainer) Destroy(child.gameObject);
            _rows.Clear();

            foreach (ScoreCategory category in System.Enum.GetValues(typeof(ScoreCategory)))
            {
                Transform targetContainer = IsLeftColumn(category) ? _leftBlockContainer : _rightBlockContainer;
                ScoreRowView newRow = Instantiate(_rowPrefab, targetContainer, false);
                
                // --- NEU: Lokalisierung laden ---
                // Generiert z.B. "cat_threeofakind"
                string locKey = $"cat_{category.ToString().ToLower()}"; 
                string displayName = Services.LocalizationService.Instance.GetText(locKey);
                
                newRow.Initialize(category, displayName);
                newRow.OnRowClicked += (cat) => OnCategoryClicked?.Invoke(cat);
                _rows.Add(category, newRow);
            }

            if (_bonusRowPrefab != null)
            {
                _bonusRowInstance = Instantiate(_bonusRowPrefab, _leftBlockContainer, false);
                _bonusRowInstance.transform.SetAsLastSibling(); 
                _bonusRowInstance.Initialize(63);
                _bonusRowInstance.OnClaimClicked += () => OnBonusClaimClicked?.Invoke();
            }

            UpdateTotals(0, 0, 0);
        }

        // --- NEU: Wird vom GameController aufgerufen, wenn die Sprache umschaltet ---
        public void UpdateTranslations()
        {
            foreach (var kvp in _rows)
            {
                ScoreCategory category = kvp.Key;
                string locKey = $"cat_{category.ToString().ToLower()}";
                string localizedName = Services.LocalizationService.Instance.GetText(locKey);
                kvp.Value.UpdateCategoryName(localizedName);
            }
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
            int clearedRows = 0;
            int suspiciousClears = 0;
            foreach (var row in _rows.Values)
            {
                // Wenn der Text grau ist (also noch nicht final eingetragen), löschen wir ihn
                var button = row.GetComponentInChildren<Button>();
                var scoreText = row.GetComponentInChildren<TextMeshProUGUI>();
                string beforeText = scoreText != null ? scoreText.text : "";
                if (button != null && button.interactable) 
                {
                    row.Clear();
                    clearedRows++;
                    if (beforeText != "-" && int.TryParse(beforeText, out _))
                    {
                        suspiciousClears++;
                    }
                }
            }
            // #region agent log
            AgentLog("pre-fix-3", "H9", "ScoreCardView.ClearAllPotentials", "cleared potential rows", $"{{\"clearedRows\":{clearedRows},\"suspiciousClears\":{suspiciousClears}}}");
            // #endregion
        }
        // In ScoreCardView.cs hinzufügen:
        public void ClearAllHighlights()
        {
            foreach (var row in _rows.Values)
            {
                row.SetHighlight(false);
            }
        }

        public void UpdateTotals(int upperRaw, int upperBonus, int grandTotal, bool isBonusClaimed = false)
        {
            // Der Name ist jetzt korrekt UpdateBonusState und übergibt den isClaimed Status!
            if (_bonusRowInstance != null)
            {
                _bonusRowInstance.UpdateBonusState(upperRaw, isBonusClaimed);
            }

            if (_upperBonusText != null){
                _upperBonusText.text = $"Bonus: ({upperRaw}/63)";
                _upperBonusText.color = upperRaw >= 63 ? Color.green : (ColorUtility.TryParseHtmlString("#FF3637", out var bonusColor) ? bonusColor : Color.red)    ; // Bonus-Zähler grün färben, wenn erreicht
            }
            if (_grandTotalText != null)
                _grandTotalText.text = $"{grandTotal}";
        }

        public void RefreshDisplay(ScoreCard scoreCard)
        {
            int finalRows = 0;
            int emptyRows = 0;
            foreach (var kvp in _rows)
            {
                int? score = scoreCard.GetScore(kvp.Key);
                if (score.HasValue)
                {
                    kvp.Value.SetFinalScore(score.Value);
                    finalRows++;
                }
                else
                {
                    kvp.Value.Clear();
                    emptyRows++;
                }
            }
            // #region agent log
            AgentLog("pre-fix-3", "H10", "ScoreCardView.RefreshDisplay", "scorecard refreshed", $"{{\"finalRows\":{finalRows},\"emptyRows\":{emptyRows}}}");
            // #endregion
            // HIER wird jetzt als 4. Wert übergeben, ob der Bonus schon abgeholt wurde
            UpdateTotals(scoreCard.UpperSectionRaw, scoreCard.UpperSectionBonus, scoreCard.GrandTotal, scoreCard.IsBonusClaimed);
        }
    }
}