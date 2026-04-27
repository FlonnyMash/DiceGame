using System;
using System.Collections; // NEU: Wichtig für die Coroutinen (IEnumerator)
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

        public event Action<int> OnDieClicked; 

        private int _dieIndex;
        private bool _currentIsHeld; // NEU: Hier merken wir uns den Zustand

        public void Initialize(int index)
        {
            _dieIndex = index;
            _button.onClick.AddListener(() => OnDieClicked?.Invoke(_dieIndex));
        }

        public void UpdateView(int value, bool isHeld)
        {
            _currentIsHeld = isHeld; // NEU: Zustand speichern
            _valueText.text = value.ToString();
            _backgroundImage.color = isHeld ? _heldColor : _normalColor;
        }

        // =========================================================================
        // NEU: DIE ANIMATIONS-LOGIK
        // =========================================================================

        // Startet die Wackel-Animation, falls der Würfel nicht gehalten wird
        public void AnimateRoll(int finalValue, float duration)
        {
            // Wenn der Würfel gehalten wird: Nichts tun, nicht wackeln.
            if (_currentIsHeld) return;

            // Sicherheitsnetz: Falls eine alte Animation läuft, stoppe sie.
            StopAllCoroutines(); 
            // Starte die Flimmer-Routine
            StartCoroutine(RollRoutine(finalValue, duration));
        }

        private IEnumerator RollRoutine(int finalValue, float duration)
        {
            float elapsed = 0f;
            float flimmerSpeed = 0.05f; // Wie schnell die Zahlen wechseln (alle 0.05 Sek)

            while (elapsed < duration)
            {
                // 1. Zeige eine zufällige Zahl zwischen 1 und 6 an
                int randomFace = UnityEngine.Random.Range(1, 7);
                _valueText.text = randomFace.ToString();
                
                // 2. Farbe auf normal setzen (während er wackelt, ist er nicht grau)
                _backgroundImage.color = _normalColor;

                // 3. Kurz warten
                elapsed += flimmerSpeed;
                yield return new WaitForSeconds(flimmerSpeed);
            }

            // Am Ende der Animation: Hart das echte Endergebnis setzen
            UpdateView(finalValue, _currentIsHeld);
        }
        // =========================================================================

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveAllListeners();
        }
    }
}