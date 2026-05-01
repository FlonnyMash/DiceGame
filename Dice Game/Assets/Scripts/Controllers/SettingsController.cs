using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Services;

namespace DiceGame.Controllers
{
    public class SettingsController : MonoBehaviour
    {
        // 1. DAS NEUE STRUCT (Muss innerhalb der Klasse stehen)
        [System.Serializable]
        public struct AudioToggleMapping
        {
            public Toggle toggle;
            public Image iconImage;
            public Sprite onSprite;
            public Sprite offSprite;
        }

        [Header("UI Controls - Audio")]
        [SerializeField] private AudioToggleMapping masterAudio;
        [SerializeField] private AudioToggleMapping musicAudio;
        [SerializeField] private AudioToggleMapping sfxAudio;
        [Header("UI Containers")]
        [SerializeField] private GameObject mainSettingsContainer;
        [SerializeField] private GameObject languageSelectionContainer;
        
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
            if (musicAudio.toggle != null) musicAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsMusicOn);
            if (sfxAudio.toggle != null) sfxAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsSfxOn);
            if (masterAudio.toggle != null) masterAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsMasterOn);

            // UI Status initialisieren
            ShowMainSettings();
            UpdateCurrentLanguageIcon(_localizationService.CurrentLanguage);

            // Listener hinzufügen
            if (masterAudio.toggle != null) masterAudio.toggle.onValueChanged.AddListener(OnMasterToggled);
            if (musicAudio.toggle != null) musicAudio.toggle.onValueChanged.AddListener(OnMusicToggled);
            if (sfxAudio.toggle != null) sfxAudio.toggle.onValueChanged.AddListener(OnSfxToggled);

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
            if (_localizationService != null)
            {
                UpdateCurrentLanguageIcon(_localizationService.CurrentLanguage);
            }

            // NEU: Toggles jedes Mal updaten, wenn das Menü aufgeht
            if (_settingsService != null)
            {
                _currentSettings = _settingsService.LoadSettings();
                
                if (masterAudio.toggle != null) masterAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsMasterOn);
                if (musicAudio.toggle != null) musicAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsMusicOn);
                if (sfxAudio.toggle != null) sfxAudio.toggle.SetIsOnWithoutNotify(_currentSettings.IsSfxOn);

                // Bilder updaten
                UpdateAudioIcon(masterAudio, _currentSettings.IsMasterOn);
                UpdateAudioIcon(musicAudio, _currentSettings.IsMusicOn);
                UpdateAudioIcon(sfxAudio, _currentSettings.IsSfxOn);
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

  // --- Audio Logic ---

        // NEU: Prüft, ob beide Sub-Toggles an sind. Wenn ja, geht Master an. Sonst aus.
        private void EvaluateMasterToggle()
        {
            if (_currentSettings == null) return;

            // Master soll nur AN sein, wenn Musik UND SFX an sind
            bool shouldMasterBeOn = _currentSettings.IsMusicOn || _currentSettings.IsSfxOn;

            if (_currentSettings.IsMasterOn != shouldMasterBeOn)
            {
                _currentSettings.IsMasterOn = shouldMasterBeOn;
                
                // SetIsOnWithoutNotify verhindert, dass ein künstlicher Klick ausgelöst wird! (Keine Endlosschleife)
                if (masterAudio.toggle != null) masterAudio.toggle.SetIsOnWithoutNotify(shouldMasterBeOn);
                
                UpdateAudioIcon(masterAudio, shouldMasterBeOn);
            }
        }

        private void OnMasterToggled(bool isOn)
        {
            if (_currentSettings != null)
            {
                // Master speichert seinen Zustand
                _currentSettings.IsMasterOn = isOn;
                UpdateAudioIcon(masterAudio, isOn);

                // Master zwingt Musik und SFX auf seinen eigenen Zustand
                _currentSettings.IsMusicOn = isOn;
                _currentSettings.IsSfxOn = isOn;

                // UI der Kinder updaten (ohne erneutes Event)
                if (musicAudio.toggle != null) musicAudio.toggle.SetIsOnWithoutNotify(isOn);
                if (sfxAudio.toggle != null) sfxAudio.toggle.SetIsOnWithoutNotify(isOn);

                UpdateAudioIcon(musicAudio, isOn);
                UpdateAudioIcon(sfxAudio, isOn);

                _settingsService.SaveSettings(_currentSettings);
            }
        }

        private void OnMusicToggled(bool isOn)
        {
            if (_currentSettings != null)
            {
                _currentSettings.IsMusicOn = isOn;
                UpdateAudioIcon(musicAudio, isOn);

                EvaluateMasterToggle(); // Prüft, ob Master reagieren muss

                _settingsService.SaveSettings(_currentSettings);
            }
        }

        private void OnSfxToggled(bool isOn)
        {
            if (_currentSettings != null)
            {
                _currentSettings.IsSfxOn = isOn;
                UpdateAudioIcon(sfxAudio, isOn);

                EvaluateMasterToggle(); // Prüft, ob Master reagieren muss

                _settingsService.SaveSettings(_currentSettings);
            }
        }
            
        
        private void UpdateAudioIcon(AudioToggleMapping mapping, bool isOn)
        {
            if (mapping.iconImage != null && mapping.onSprite != null && mapping.offSprite != null)
            {
                mapping.iconImage.sprite = isOn ? mapping.onSprite : mapping.offSprite;
            }
        }

    }
}