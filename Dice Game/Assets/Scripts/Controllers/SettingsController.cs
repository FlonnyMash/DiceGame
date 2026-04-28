using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Services; 

namespace DiceGame.Controllers
{
    public class SettingsController : MonoBehaviour
    {
        [Header("UI Toggles")]
        [SerializeField] private Toggle masterToggle; 
        [SerializeField] private Toggle musicToggle;
        [SerializeField] private Toggle sfxToggle;

        [Header("Master Icons")]
        [SerializeField] private Image masterIconImage; 
        [SerializeField] private Sprite masterOnSprite;
        [SerializeField] private Sprite masterOffSprite;

        [Header("Music Icons")]
        [SerializeField] private Image musicIconImage; 
        [SerializeField] private Sprite musicOnSprite;
        [SerializeField] private Sprite musicOffSprite;

        [Header("SFX Icons")]
        [SerializeField] private Image sfxIconImage; 
        [SerializeField] private Sprite sfxOnSprite;
        [SerializeField] private Sprite sfxOffSprite;

        private ISettingsService _settingsService;
        private AppSettings _currentSettings;

        private void Awake()
        {
            _settingsService = PlayerPrefsSettingsService.Instance; 
        }

        private void Start()
        {
            _currentSettings = _settingsService.LoadSettings();

            // UI-Zustände initial setzen
            if (musicToggle != null) musicToggle.SetIsOnWithoutNotify(_currentSettings.IsMusicOn);
            if (sfxToggle != null) sfxToggle.SetIsOnWithoutNotify(_currentSettings.IsSfxOn);

            // Master ist an, wenn Musik ODER SFX an sind
            bool isAnyOn = _currentSettings.IsMusicOn || _currentSettings.IsSfxOn;
            if (masterToggle != null) masterToggle.SetIsOnWithoutNotify(isAnyOn);

            // Icons beim Start setzen
            UpdateIcons();

            // Listener hinzufügen
            if (masterToggle != null) masterToggle.onValueChanged.AddListener(OnMasterToggled);
            if (musicToggle != null) musicToggle.onValueChanged.AddListener(OnMusicToggled);
            if (sfxToggle != null) sfxToggle.onValueChanged.AddListener(OnSfxToggled);
        }

        private void OnMasterToggled(bool isOn)
        {
            _currentSettings.IsMusicOn = isOn;
            _currentSettings.IsSfxOn = isOn;

            if (musicToggle != null) musicToggle.SetIsOnWithoutNotify(isOn);
            if (sfxToggle != null) sfxToggle.SetIsOnWithoutNotify(isOn);

            UpdateIcons();
            _settingsService.SaveSettings(_currentSettings);
        }

        private void OnMusicToggled(bool isOn)
        {
            _currentSettings.IsMusicOn = isOn;
            CheckMasterToggleState();
            UpdateIcons();
            _settingsService.SaveSettings(_currentSettings);
        }

        private void OnSfxToggled(bool isOn)
        {
            _currentSettings.IsSfxOn = isOn;
            CheckMasterToggleState();
            UpdateIcons();
            _settingsService.SaveSettings(_currentSettings);
        }

        private void CheckMasterToggleState()
        {
            if (masterToggle == null) return;
            bool isAnyOn = _currentSettings.IsMusicOn || _currentSettings.IsSfxOn;
            masterToggle.SetIsOnWithoutNotify(isAnyOn);
        }

        private void UpdateIcons()
        {
            // 1. MASTER ICON
            if (masterIconImage != null)
            {
                bool isAnyOn = _currentSettings.IsMusicOn || _currentSettings.IsSfxOn;
                masterIconImage.sprite = isAnyOn ? masterOnSprite : masterOffSprite;
                
                // Alpha-Sicherung
                Color c = masterIconImage.color;
                c.a = 1f;
                masterIconImage.color = c;
            }

            // 2. MUSIC ICON
            if (musicIconImage != null)
            {
                musicIconImage.sprite = _currentSettings.IsMusicOn ? musicOnSprite : musicOffSprite;
                Color c = musicIconImage.color;
                c.a = 1f;
                musicIconImage.color = c;
            }

            // 3. SFX ICON
            if (sfxIconImage != null)
            {
                sfxIconImage.sprite = _currentSettings.IsSfxOn ? sfxOnSprite : sfxOffSprite;
                Color c = sfxIconImage.color;
                c.a = 1f;
                sfxIconImage.color = c;
            }
        }
    }
}