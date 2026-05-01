using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Services;

namespace DiceGame.Controllers
{
    public class SettingsController : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject mainSettingsContainer;
        [SerializeField] private GameObject languageSelectionContainer;

        [Header("UI Controls - Audio")]
        [SerializeField] private Toggle masterToggle;
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;
        
        [Header("UI Controls - Language Main")]
        [SerializeField] private Button openLanguagePanelButton;
        [SerializeField] private Image currentLanguageIcon;
        
        [Header("UI Controls - Language Selection")]
        [SerializeField] private Button closeLanguagePanelButton;
        [SerializeField] private LanguageButtonMapping[] languageButtons;
        [System.Serializable]
        public struct LanguageButtonMapping
        {
            public SupportedLanguage language;
            public Button button;
            public Sprite flagSprite;
        }


        private ISettingsService _settingsService;
        private ILocalizationService _localizationService;
        private AppSettings _currentSettings;

        private void Awake()
        {
            _settingsService = PlayerPrefsSettingsService.Instance;
            _localizationService = LocalizationService.Instance;
        }

        private void Start()
        {
            _currentSettings = _settingsService.LoadSettings();

            // Audio initialisieren
            if (musicToggle != null) musicToggle.SetIsOnWithoutNotify(_currentSettings.IsMusicOn);
            if (sfxToggle != null) sfxToggle.SetIsOnWithoutNotify(_currentSettings.IsSfxOn);
            
            // UI Status initialisieren
            ShowMainSettings();
            UpdateCurrentLanguageIcon(_localizationService.CurrentLanguage);

            // Listener hinzufügen
            if (masterToggle != null) masterToggle.onValueChanged.AddListener(OnMasterToggled);
            if (musicToggle != null) musicToggle.onValueChanged.AddListener(OnMusicToggled);
            if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(OnSfxToggled);

            // Language Panel Navigation
            if (openLanguagePanelButton != null) openLanguagePanelButton.onClick.AddListener(ShowLanguageSelection);
            if (closeLanguagePanelButton != null) closeLanguagePanelButton.onClick.AddListener(ShowMainSettings);

           foreach (var mapping in languageButtons)
            {
                if (mapping.button != null)
                {
                    // WICHTIG: Wir brauchen eine lokale Kopie der Sprache für den Lambda-Ausdruck
                    SupportedLanguage lang = mapping.language;
                    mapping.button.onClick.AddListener(() => SetLanguage(lang));
                }
            }
            _localizationService.OnLanguageChanged += HandleLanguageChanged;
        }
        private void OnEnable()
        {
            // Wenn das Settings-Fenster aktiviert wird, updaten wir sofort das Icon
            if (_localizationService != null)
            {
                UpdateCurrentLanguageIcon(_localizationService.CurrentLanguage);
            }
        }

        private void OnDestroy()
        {
            if (_localizationService != null)
            {
                _localizationService.OnLanguageChanged -= HandleLanguageChanged;
            }
            
            // Language Panel Navigation aufräumen
            if (openLanguagePanelButton != null) openLanguagePanelButton.onClick.RemoveAllListeners();
            if (closeLanguagePanelButton != null) closeLanguagePanelButton.onClick.RemoveAllListeners();

            // NEU: Alle Buttons im Array dynamisch aufräumen
            if (languageButtons != null)
            {
                foreach (var mapping in languageButtons)
                {
                    if (mapping.button != null)
                    {
                        mapping.button.onClick.RemoveAllListeners();
                    }
                }
            }
        }

        // --- Panel Navigation ---

        private void ShowMainSettings()
        {
            if (mainSettingsContainer != null) mainSettingsContainer.SetActive(true);
            if (languageSelectionContainer != null) languageSelectionContainer.SetActive(false);
        }

        private void ShowLanguageSelection()
        {
            if (mainSettingsContainer != null) mainSettingsContainer.SetActive(false);
            if (languageSelectionContainer != null) languageSelectionContainer.SetActive(true);
        }

        // --- Language Logic ---

        private void SetLanguage(SupportedLanguage newLanguage)
        {
            _localizationService.SetLanguage(newLanguage);
            ShowMainSettings(); // Panel schließen nach Auswahl
        }

        private void HandleLanguageChanged()
        {
            UpdateCurrentLanguageIcon(_localizationService.CurrentLanguage);
        }

        private void UpdateCurrentLanguageIcon(SupportedLanguage currentLanguage)
        {
            Debug.Log($"[SETTINGS] Versuche Icon für {currentLanguage} zu setzen.");
            if (currentLanguageIcon != null && languageButtons != null)
            {
                foreach (var mapping in languageButtons)
                {
                    if (mapping.language == currentLanguage)
                    {
                        currentLanguageIcon.sprite = mapping.flagSprite;
                        Debug.Log($"[SETTINGS] Icon erfolgreich auf {mapping.language} geändert.");
                        return;
                    }
                }
                Debug.LogWarning("[SETTINGS] Kein passendes Flaggen-Sprite im Array gefunden!");
            }
        }

        // --- Audio Logic (Placeholder) ---
        private void OnMasterToggled(bool isOn) { /* ... */ }
        private void OnMusicToggled(bool isOn) { /* ... */ }
        private void OnSfxToggled(bool isOn) { /* ... */ }
    }
}