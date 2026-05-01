using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Rules;

namespace DiceGame.UI.Views
{
    public class ScoreRowView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _categoryNameText;
        [SerializeField] private TextMeshProUGUI _scoreText;
        [SerializeField] private Button _selectButton;
        
        [Header("Optional: Button Background")]
        [SerializeField] private Image _buttonImage;
        [SerializeField] private Sprite _normalSprite;
        [SerializeField] private Sprite _completedSprite;

        [Header("Animation")]
        [SerializeField] private Animator _animator; 
        private const string ANIM_TRIGGER_SELECTED = "OnSelected"; 
        
        [Header("Colors")]
        [SerializeField] private Color _filledColor = Color.black;
        [SerializeField] private Color _potentialColor = Color.gray;

        public ScoreCategory Category { get; private set; }
        
        public event Action<ScoreCategory> OnRowClicked;

        private void Awake() 
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_selectButton == null) _selectButton = GetComponent<Button>();
        }

        public void Initialize(ScoreCategory category, string displayName)
        {
            Category = category;
            _categoryNameText.text = displayName;
            
            _selectButton.onClick.RemoveAllListeners();
            _selectButton.onClick.AddListener(() => 
            {
                // --- NEU: Die Animation feuert jetzt NUR NOCH bei einem echten Klick! ---
                if (_animator != null)
                {
                    _animator.SetTrigger(ANIM_TRIGGER_SELECTED);
                }
                
                OnRowClicked?.Invoke(Category);
            });
            Clear();
        }

        public void ShowPotentialScore(int potentialScore)
        {
            _scoreText.text = potentialScore.ToString();
            _scoreText.color = _potentialColor;
            _selectButton.interactable = true; 
        }

        public void SetFinalScore(int score)
        {
            _scoreText.text = score.ToString();
            _scoreText.color = _filledColor;
            _selectButton.interactable = false; 
            
            if (_buttonImage != null && _completedSprite != null)
            {
                _buttonImage.sprite = _completedSprite;
            }
            // (Animations-Trigger hier gelöscht, da er sonst von RefreshDisplay immer wieder aufgerufen wird)
        }

        public void Clear()
        {
            _scoreText.text = "-";
            _scoreText.color = _potentialColor;
            _selectButton.interactable = false;
            
            if (_buttonImage != null && _normalSprite != null)
            {
                _buttonImage.sprite = _normalSprite;
            }
        }

        private void OnDestroy()
        {
            if (_selectButton != null)
            {
                _selectButton.onClick.RemoveAllListeners();
            }
        }

        // Erlaubt uns, den Namen nachträglich zu ändern, wenn die Sprache umschaltet
        public void UpdateCategoryName(string localizedName)
        {
            if (_categoryNameText != null)
            {
                _categoryNameText.text = localizedName;
            }
        }
    }
}