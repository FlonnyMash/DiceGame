using System;
using System.Collections.Generic;
using System.IO;
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
            string langCode = (language == SupportedLanguage.German) ? "de" : "en";
            string filePath = Path.Combine(Application.streamingAssetsPath, "Localization", $"{langCode}.json");

            if (File.Exists(filePath))
            {
                try 
                {
                    string json = File.ReadAllText(filePath);
                    _translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    Debug.Log($"[Localization] Successfully loaded: {langCode}");
                }
                catch (Exception e) 
                {
                    Debug.LogError($"[Localization] Error parsing JSON: {e.Message}");
                    _translations = new Dictionary<string, string>();
                }
            }
            else
            {
                Debug.LogError($"[Localization] File not found: {filePath}");
                _translations = new Dictionary<string, string>();
            }
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