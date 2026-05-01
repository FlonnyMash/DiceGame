using UnityEngine;
using TMPro; // Für TextMeshPro
using DiceGame.Services;

namespace DiceGame.UI
{
    // Stellt sicher, dass das Skript nur funktioniert, wenn auch ein Text-Objekt da ist
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedUIText : MonoBehaviour
    {
        [Tooltip("Der Key aus der JSON-Datei, z.B. 'menu_play_button'")]
        [SerializeField] private string localizationKey;
        
        private TextMeshProUGUI _textComponent;

        private void Awake()
        {
            _textComponent = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            // Sofort beim Start übersetzen
            UpdateText();

            // Beim Service registrieren, falls der Spieler die Sprache in den Settings ändert
            LocalizationService.Instance.OnLanguageChanged += UpdateText;
        }

        private void OnDestroy()
        {
            // WICHTIG: Immer abmelden, um Memory Leaks zu vermeiden
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= UpdateText;
            }
        }

        private void UpdateText()
        {
            if (string.IsNullOrEmpty(localizationKey))
            {
                Debug.LogWarning($"[LocalizedUIText] Kein Key gesetzt auf {gameObject.name}", this);
                return;
            }

            // Holt den übersetzten Text aus unserem JSON-Service
            _textComponent.text = LocalizationService.Instance.GetText(localizationKey);
        }
    }
}