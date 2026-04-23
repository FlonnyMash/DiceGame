using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Rules;

namespace DiceGame.UI.Views
{
    public class ScoreRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _categoryNameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Button _selectButton;
        
        [Header("Colors")]
        [SerializeField] private Color _filledColor = Color.black;
        [SerializeField] private Color _potentialColor = Color.gray;

        public ScoreCategory Category { get; private set; }
        
        // Sagt dem Controller: "Der Spieler will hier etwas eintragen!"
        public event Action<ScoreCategory> OnRowClicked;

        // In ScoreRowView.cs
        public void Initialize(ScoreCategory category, string displayName)
        {
            Category = category;
            _categoryNameText.text = displayName;
            
            // Falls der Button jetzt auf dem gleichen Objekt wie das Skript liegt:
            if (_selectButton == null) _selectButton = GetComponent<Button>();
            
            _selectButton.onClick.AddListener(() => OnRowClicked?.Invoke(Category));
            Clear();
        }

        // Zeigt an, was man bekommen WÜRDE, wenn man jetzt klickt (Vorschau)
        public void ShowPotentialScore(int potentialScore)
        {
            _scoreText.text = potentialScore.ToString();
            _scoreText.color = _potentialColor;
            _selectButton.interactable = true; // Klickbar machen
        }

        // Wird aufgerufen, wenn die Punkte final eingetragen wurden
        public void SetFinalScore(int score)
        {
            _scoreText.text = score.ToString();
            _scoreText.color = _filledColor;
            _selectButton.interactable = false; // Nach dem Eintragen nicht mehr klickbar!
        }

        // Wenn noch nicht gewürfelt wurde oder die Runde neu startet
        public void Clear()
        {
            _scoreText.text = "-";
            _scoreText.color = _potentialColor;
            _selectButton.interactable = false;
        }

        private void OnDestroy(){
    // Wir prüfen erst, ob der Button überhaupt (noch) existiert
            if (_selectButton != null)
                {
                    _selectButton.onClick.RemoveAllListeners();
                }
        }
    }
}