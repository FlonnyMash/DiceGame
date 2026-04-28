using UnityEngine;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using System;

namespace DiceGame.Services
{
    public class PlayerPrefsSettingsService : ISettingsService
    {
        public event Action<AppSettings> OnSettingsChanged;

        private const string PREF_MUSIC_VOL = "MusicVolume";
        private const string PREF_SFX_VOL = "SfxVolume";

        public AppSettings LoadSettings()
        {
            return new AppSettings
            {
                MusicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 1.0f),
                SfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 1.0f)
            };
        }

        public void SaveSettings(AppSettings settings)
        {
            PlayerPrefs.SetFloat(PREF_MUSIC_VOL, settings.MusicVolume);
            PlayerPrefs.SetFloat(PREF_SFX_VOL, settings.SfxVolume);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke(settings);
        }
    }
}