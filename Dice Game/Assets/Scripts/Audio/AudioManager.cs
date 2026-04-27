using UnityEngine;

namespace DiceGame.Audio
{
    public class AudioManager : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnStartup()
        {
            // Das triggert die Instance-Logik und erstellt den Manager sofort
            var trigger = Instance;
        }
        
        private static AudioManager _instance;

        // Das ist das "Tor" zum Manager
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 1. Schauen, ob er schon in der Szene ist
                    _instance = FindObjectOfType<AudioManager>();

                    // 2. Wenn nicht, aus dem Resources-Ordner laden
                    if (_instance == null)
                    {
                        GameObject prefab = Resources.Load<GameObject>("AudioManager");
                        if (prefab != null)
                        {
                            GameObject go = Instantiate(prefab);
                            _instance = go.GetComponent<AudioManager>();
                            _instance.name = "AudioManager (Auto-Generated)";
                        }
                        else
                        {
                            Debug.LogError("AudioManager-Prefab nicht im 'Resources' Ordner gefunden!");
                        }
                    }
                }
                return _instance;
            }
        }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        private void Awake()
        {
            // Singleton-Sicherung
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

        public void PlaySFX(AudioClip clip, bool randomizePitch = false)
        {
            if (clip != null && _sfxSource != null)
            {
                if (randomizePitch)
                {
                    // Verändert die Tonhöhe minimal zwischen 0.9 (tiefer) und 1.1 (höher)
                    _sfxSource.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                }
                else
                {
                    _sfxSource.pitch = 1.0f; // Normaler Pitch
                }

                _sfxSource.PlayOneShot(clip);
            }
        }
    }
}