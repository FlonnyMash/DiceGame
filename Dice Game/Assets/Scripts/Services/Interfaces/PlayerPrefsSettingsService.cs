using UnityEngine;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using System;

namespace DiceGame.Services
{
    public class PlayerPrefsSettingsService : ISettingsService
    {
        private static PlayerPrefsSettingsService _instance;
        public static PlayerPrefsSettingsService Instance => _instance ??= new PlayerPrefsSettingsService();

        public event Action<AppSettings> OnSettingsChanged;

        private const string PREF_MUSIC_ON = "IsMusicOn";
        private const string PREF_SFX_ON = "IsSfxOn";
        private const string PREF_LANGUAGE = "SelectedLanguage"; // Neuer Key

        public AppSettings LoadSettings()
        {
            AppSettings settings = new AppSettings();
            
            settings.IsMusicOn = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1;
            settings.IsSfxOn = PlayerPrefs.GetInt(PREF_SFX_ON, 1) == 1;

            // Spracherkennung: Wenn kein Wert da ist, nimm die Systemsprache
            if (PlayerPrefs.HasKey(PREF_LANGUAGE))
            {
                settings.Language = (SupportedLanguage)PlayerPrefs.GetInt(PREF_LANGUAGE);
            }
            else
            {
                settings.Language = GetDeviceDefaultLanguage();
            }

            return settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            PlayerPrefs.SetInt(PREF_MUSIC_ON, settings.IsMusicOn ? 1 : 0);
            PlayerPrefs.SetInt(PREF_SFX_ON, settings.IsSfxOn ? 1 : 0);
            PlayerPrefs.SetInt(PREF_LANGUAGE, (int)settings.Language);
            PlayerPrefs.Save();
            
            OnSettingsChanged?.Invoke(settings);
        }

        private SupportedLanguage GetDeviceDefaultLanguage()
        {
            if (Application.systemLanguage == SystemLanguage.German)
                return SupportedLanguage.German;
            
            return SupportedLanguage.English;
        }
    }
}