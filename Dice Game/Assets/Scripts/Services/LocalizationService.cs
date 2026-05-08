using System;
using System.Collections.Generic;
using UnityEngine;
using DiceGame.Services.Interfaces;
using Newtonsoft.Json;

namespace DiceGame.Services
{
    public class LocalizationService : ILocalizationService
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event Action OnLanguageChanged;
        
        private SupportedLanguage _currentLanguage;
        public SupportedLanguage CurrentLanguage => _currentLanguage;

        private Dictionary<string, string> _translations = new();
        private const string LanguagePrefKey = "SelectedLanguage";

        public LocalizationService()
        {
            // Lädt die Sprache über unseren sauberen Settings-Service
            var settings = PlayerPrefsSettingsService.Instance.LoadSettings();
            _currentLanguage = settings.Language;
            LoadLanguage(_currentLanguage);
        }

        private SupportedLanguage GetDeviceLanguage()
        {
            if (Application.systemLanguage == SystemLanguage.German)
                return SupportedLanguage.German;
            
            return SupportedLanguage.English;
        }

        public void SetLanguage(SupportedLanguage language)
        {
            _currentLanguage = language;
            
            var settings = PlayerPrefsSettingsService.Instance.LoadSettings();
            settings.Language = language;
            PlayerPrefsSettingsService.Instance.SaveSettings(settings);
            
            LoadLanguage(language);
            OnLanguageChanged?.Invoke();
        }

        private void LoadLanguage(SupportedLanguage language)
        {
            string langCode = GetLangCode(language);

            // Resources.Load works synchronously on ALL platforms (incl. Android/iOS/WebGL).
            // StreamingAssets + System.IO.File would silently fail on Android, where the
            // files live inside the compressed APK/AAB and are not accessible via File API.
            TextAsset jsonAsset = Resources.Load<TextAsset>($"Localization/{langCode}");

            if (jsonAsset != null)
            {
                try
                {
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonAsset.text)
                                    ?? new Dictionary<string, string>();
                    Debug.Log($"[Localization] Successfully loaded: {langCode} ({_translations.Count} entries)");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Localization] Error parsing JSON for '{langCode}': {e.Message}");
                    _translations = new Dictionary<string, string>();
                }
                finally
                {
                    Resources.UnloadAsset(jsonAsset);
                }
            }
            else
            {
                Debug.LogError($"[Localization] Resources/Localization/{langCode}.json not found.");
                _translations = new Dictionary<string, string>();
            }
        }

        private string GetLangCode(SupportedLanguage language)
        {
            return language switch
            {
                SupportedLanguage.German => "de",
                SupportedLanguage.English => "en",
                SupportedLanguage.Spanish => "es",
                SupportedLanguage.French => "fr",
                SupportedLanguage.Italian => "it",
                SupportedLanguage.Portuguese => "pt",
                _ => "en" // Fallback
            };
        }

        public string GetText(string key, params object[] args)
        {
            if (_translations != null && _translations.TryGetValue(key, out string text))
            {
                return args != null && args.Length > 0 ? string.Format(text, args) : text;
            }
            return $"[{key}]";
        }
    }
}