using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.EventSystems;
using System;
using System.Collections; // Wichtig für die Animation

namespace DiceGame.UI.Views
{
    public class DieView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _dieImage; 
        [SerializeField] private GameObject _heldHighlight; 

        [Header("Dice Faces (1 to 6)")]
        [SerializeField] private Sprite[] _diceFaces; 

        // Fix Fehler 2: Wir senden jetzt wieder einen 'int' (den Index) beim Klicken
        public event Action<int> OnDieClicked; 

        private int _dieIndex;

        // Fix Fehler 1: Die fehlende Initialize Methode
        public void Initialize(int index)
        {
            _dieIndex = index;
        }

        public void UpdateView(int value, bool isHeld)
        {
            // Das richtige Bild setzen
            if (value >= 1 && value <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[value - 1];
            }

            // Den "Gehalten"-Status anzeigen
            if (_heldHighlight != null)
            {
                _heldHighlight.SetActive(isHeld);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Sende den Index an den GameController
            OnDieClicked?.Invoke(_dieIndex);
        }

// Wir fordern jetzt beide Werte an: den finalen Wert und die Dauer
        public void AnimateRoll(int finalValue, float duration)
        {
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

            // 2. Das große Finale: Den echten, erwürfelten Wert anzeigen!
            if (finalValue >= 1 && finalValue <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[finalValue - 1];
            }
        }
    }
}