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
        [SerializeField] private Button englishLanguageButton;
        [SerializeField] private Button germanLanguageButton;

        [Header("Language Assets")]
        [SerializeField] private Sprite englishFlagSprite;
        [SerializeField] private Sprite germanFlagSprite;

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

            // Language Selection
            if (englishLanguageButton != null) englishLanguageButton.onClick.AddListener(() => SetLanguage(SupportedLanguage.English));
            if (germanLanguageButton != null) germanLanguageButton.onClick.AddListener(() => SetLanguage(SupportedLanguage.German));
            
            // Wir abonnieren den Service, falls die Sprache von woanders geändert wird
            _localizationService.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDestroy()
        {
            if (_localizationService != null)
            {
                _localizationService.OnLanguageChanged -= HandleLanguageChanged;
            }
            
            // Event Listener aufräumen
            if (openLanguagePanelButton != null) openLanguagePanelButton.onClick.RemoveAllListeners();
            if (closeLanguagePanelButton != null) closeLanguagePanelButton.onClick.RemoveAllListeners();
            if (englishLanguageButton != null) englishLanguageButton.onClick.RemoveAllListeners();
            if (germanLanguageButton != null) germanLanguageButton.onClick.RemoveAllListeners();
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
            if (currentLanguageIcon != null)
            {
                currentLanguageIcon.sprite = currentLanguage == SupportedLanguage.English 
                    ? englishFlagSprite 
                    : germanFlagSprite;
            }
        }

        // --- Audio Logic (Placeholder) ---
        private void OnMasterToggled(bool isOn) { /* ... */ }
        private void OnMusicToggled(bool isOn) { /* ... */ }
        private void OnSfxToggled(bool isOn) { /* ... */ }
    }
}