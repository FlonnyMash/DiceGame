using UnityEngine;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Services;

namespace DiceGame.Audio
{
    public class AudioManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnStartup()
        {
            var trigger = Instance;
        }
        
        private static AudioManager _instance;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<AudioManager>();

                    if (_instance == null)
                    {
                        GameObject prefab = Resources.Load<GameObject>("AudioManager");
                        if (prefab != null)
                        {
                            GameObject go = Instantiate(prefab);
                            _instance = go.GetComponent<AudioManager>();
                            _instance.name = "AudioManager (Auto-Generated)";
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        private ISettingsService _settingsService;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            _settingsService = PlayerPrefsSettingsService.Instance;

            ApplyAudioSettings(_settingsService.LoadSettings());
            _settingsService.OnSettingsChanged += ApplyAudioSettings;
        }

        private void OnDestroy()
        {
            if (_settingsService != null)
            {
                _settingsService.OnSettingsChanged -= ApplyAudioSettings;
            }
        }

        private void ApplyAudioSettings(AppSettings settings)
        {
            // Die "Mute"-Eigenschaft schaltet den Ton komplett aus (true) oder an (false)
            if (_musicSource != null)
            {
                _musicSource.mute = !settings.IsMusicOn; 
            }

            if (_sfxSource != null)
            {
                _sfxSource.mute = !settings.IsSfxOn;
            }
        }

        public void PlaySFX(AudioClip clip, bool randomizePitch = false)
        {
            // Wenn SFX ausgestellt ist, spielen wir gar nicht erst ab
            if (clip != null && _sfxSource != null && !_sfxSource.mute)
            {
                if (randomizePitch)
                {
                    _sfxSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                }
                else
                {
                    _sfxSource.pitch = 1.0f; 
                }

                _sfxSource.PlayOneShot(clip);
            }
        }
    }
}