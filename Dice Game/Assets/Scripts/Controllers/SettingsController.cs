using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Services; 

namespace DiceGame.Controllers
{
    public class SettingsController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        private ISettingsService _settingsService;
        private AppSettings _currentSettings;

        private void Awake()
        {
            // Initialisiere den Service
            _settingsService = new PlayerPrefsSettingsService(); 
        }

        private void Start()
        {
            // Settings laden und UI aktualisieren
            _currentSettings = _settingsService.LoadSettings();

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(_currentSettings.MusicVolume);
            
            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(_currentSettings.SfxVolume);

            // Listener an die Slider hängen
            musicSlider?.onValueChanged.AddListener(OnMusicVolumeChanged);
            sfxSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        private void OnMusicVolumeChanged(float value)
        {
            _currentSettings.MusicVolume = value;
            _settingsService.SaveSettings(_currentSettings);
        }

        private void OnSfxVolumeChanged(float value)
        {
            _currentSettings.SfxVolume = value;
            _settingsService.SaveSettings(_currentSettings);
        }
    }
}