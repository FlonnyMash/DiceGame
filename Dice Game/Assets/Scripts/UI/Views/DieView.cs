using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.EventSystems;
using System;
using System.Collections;

namespace DiceGame.UI.Views
{
    public class DieView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _dieImage; 
        [SerializeField] private GameObject _heldHighlight; 

        [Header("Dice Faces (1 to 6)")]
        [SerializeField] private Sprite[] _diceFaces; 

        public event Action<int> OnDieClicked; 

        private int _dieIndex;
        private bool _isHeld; // Unser "Gedächtnis"

        public void Initialize(int index)
        {
            _dieIndex = index;
        }

        public void UpdateView(int value, bool isHeld)
        {
            // Wir merken uns sofort, ob der Würfel gehalten wird
            _isHeld = isHeld; 

            // Das richtige Bild setzen
            if (value >= 1 && value <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[value - 1];
            }

            // Den "Gehalten"-Status (den Rahmen) anzeigen
            if (_heldHighlight != null)
            {
                _heldHighlight.SetActive(isHeld);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Dem Controller sagen, welcher Würfel geklickt wurde
            OnDieClicked?.Invoke(_dieIndex);
        }

        public void AnimateRoll(int finalValue, float duration)
        {
            // Türsteher: Wenn der Würfel gehalten wird, brechen wir hier sofort ab!
            if (_isHeld) 
            {
                return; 
            }

            StartCoroutine(RollAnimationRoutine(finalValue, duration));
        }

        private IEnumerator RollAnimationRoutine(int finalValue, float duration)
        {
            float elapsed = 0f;
            
            // 1. Die wilde "Roll"-Phase (zufällige Bilder)
            while (elapsed < duration)
            {
                if (_diceFaces.Length > 0)
                {
                    int randomFace = UnityEngine.Random.Range(0, _diceFaces.Length);
                    _dieImage.sprite = _diceFaces[randomFace];
                }
                
                yield return new WaitForSeconds(0.05f);
                elapsed += 0.05f;
            }

            // 2. Das große Finale: Den echten Wert anzeigen
            if (finalValue >= 1 && finalValue <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[finalValue - 1];
            }
        }
    }
}