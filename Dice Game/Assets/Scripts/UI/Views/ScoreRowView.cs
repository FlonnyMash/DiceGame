using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Rules;

namespace DiceGame.UI.Views
{
    [RequireComponent(typeof(LayoutElement))]
    public class ScoreRowView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform _visualContainer; 
        [SerializeField] private TextMeshProUGUI _categoryNameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Button _selectButton;
        
        [Header("Animation & Depth")]
        [SerializeField] private Animator _animator; 
        [SerializeField] private Canvas _rowCanvas; 
        [SerializeField] private GraphicRaycaster _raycaster; 

        // Wieder exakt auf deine Schreibweise angepasst!
        private static readonly int SelectedTriggerHash = Animator.StringToHash("OnSelected");
        private static readonly int OffSelectTriggerHash = Animator.StringToHash("OffSelect"); 
        private static readonly int HighlightedBoolHash = Animator.StringToHash("IsHighlighted");

        [Header("Colors")]
        [SerializeField] private Color _filledColor = Color.black;
        [SerializeField] private Color _potentialColor = Color.gray;

        public ScoreCategory Category { get; private set; }
        public event Action<ScoreCategory> OnRowClicked;

        private void Awake() 
        {
            if (_visualContainer != null)
            {
                if (_rowCanvas == null) _rowCanvas = _visualContainer.GetComponent<Canvas>();
                if (_raycaster == null) _raycaster = _visualContainer.GetComponent<GraphicRaycaster>();
                if (_animator == null) _animator = _visualContainer.GetComponent<Animator>();
            }
            if (_selectButton == null) _selectButton = GetComponentInChildren<Button>();
        }

        public void Initialize(ScoreCategory category, string displayName)
        {
            Category = category;
            _categoryNameText.text = displayName;
            
            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(() => 
            {
                SetHighlight(false);
                
                if (_animator != null)
                {
                    _animator.ResetTrigger(OffSelectTriggerHash);
                    _animator.SetTrigger(SelectedTriggerHash);
                }
                
                _selectButton.interactable = false;
                OnRowClicked?.Invoke(Category);
            });
            Clear();
        }

        public void ShowPotentialScore(int potentialScore)
        {
            _scoreText.text = potentialScore.ToString();
            _scoreText.color = _potentialColor;
            _selectButton.interactable = true; 
            SetHighlight(potentialScore > 0);
        }

        public void SetFinalScore(int score)
        {
            _scoreText.text = score.ToString();
            _scoreText.color = _filledColor;
            
            _selectButton.interactable = false; 
            SetHighlight(false);

            if (_animator != null)
            {
                // Verhindert, dass alte Trigger in der Queue hängen bleiben
                _animator.ResetTrigger(OffSelectTriggerHash);
                _animator.SetTrigger(SelectedTriggerHash);
            }
        }

        public void Clear()
        {
            _scoreText.text = "-";
            _scoreText.color = _potentialColor;
            _selectButton.interactable = false;
            SetHighlight(false);

            if (_animator != null)
            {
                // Löscht OnSelected und erzwingt das Zurücksetzen ins Idle
                _animator.ResetTrigger(SelectedTriggerHash);
                _animator.SetTrigger(OffSelectTriggerHash);
            }
        }

        public void SetHighlight(bool active)
        {
            if (_animator != null)
                _animator.SetBool(HighlightedBoolHash, active);

            if (_rowCanvas != null)
            {
                _rowCanvas.overrideSorting = active;
                _rowCanvas.sortingOrder = active ? 10 : 0;
            }

            if (_raycaster != null)
            {
                _raycaster.enabled = active;
            }
        }

        public void UpdateCategoryName(string localizedName)
        {
            if (_categoryNameText != null) _categoryNameText.text = localizedName;
        }
    }
}