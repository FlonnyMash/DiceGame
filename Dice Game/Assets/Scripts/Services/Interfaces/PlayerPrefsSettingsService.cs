using UnityEngine;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using System;

namespace DiceGame.Services
{
    public class PlayerPrefsSettingsService : ISettingsService
    {
        private static PlayerPrefsSettingsService _instance;
        public static PlayerPrefsSettingsService Instance
        {
            get
            {
                if (_instance == null) _instance = new PlayerPrefsSettingsService();
                return _instance;
            }
        }

        public event Action<AppSettings> OnSettingsChanged;

        private const string PREF_MUSIC_ON = "IsMusicOn";
        private const string PREF_SFX_ON = "IsSfxOn";

        public AppSettings LoadSettings()
        {
            return new AppSettings
            {
                // Wenn nichts gespeichert ist, nehmen wir 1 (True/An)
                IsMusicOn = PlayerPrefs.GetInt(PREF_MUSIC_ON, 1) == 1,
                IsSfxOn = PlayerPrefs.GetInt(PREF_SFX_ON, 1) == 1
            };
        }

        public void SaveSettings(AppSettings settings)
        {
            PlayerPrefs.SetInt(PREF_MUSIC_ON, settings.IsMusicOn ? 1 : 0);
            PlayerPrefs.SetInt(PREF_SFX_ON, settings.IsSfxOn ? 1 : 0);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke(settings);
        }
    }
}