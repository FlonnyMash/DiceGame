using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Für TextMeshPro

namespace DiceGame.UI.Views
{
    public class DieView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private Image _backgroundImage;

        [Header("Colors")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _heldColor = Color.gray;

        // Event, das dem Controller sagt: "Hey, ich wurde geklickt!"
        public event Action<int> OnDieClicked; 

        private int _dieIndex;

        // Wird vom Controller beim Start aufgerufen
        public void Initialize(int index)
        {
            _dieIndex = index;
            
            // Unity-Button Klick an unser C#-Event weiterleiten
            _button.onClick.AddListener(() => OnDieClicked?.Invoke(_dieIndex));
        }

        // Wird aufgerufen, wenn sich der Core-Würfel ändert
        public void UpdateView(int value, bool isHeld)
        {
            _valueText.text = value.ToString();
            _backgroundImage.color = isHeld ? _heldColor : _normalColor;
        }

        private void OnDestroy()
        {
            _button.onClick.RemoveAllListeners();
        }
    }
}