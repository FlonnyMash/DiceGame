using UnityEngine;

namespace DiceGame.Services
{
    public static class HapticService
    {
        /// <summary>
        /// Löst eine Vibration aus. 
        /// HINWEIS FÜR SPÄTER (Apple Arcade): Unitys 'Handheld.Vibrate()' erzeugt eine relativ lange, starke Standard-Vibration. 
        /// Für HD-Rumble und feines haptisches Feedback solltest du hier später ein iOS/Android Haptics Plugin 
        /// (z.B. Unitys neues Haptics-Paket) integrieren.
        /// </summary>
        public static void PlayShakeHaptic()
        {
            // Optional für später: Hier kannst du auch abfragen, ob der Spieler 
            // Vibrationen in den AppSettings deaktiviert hat!
            
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}