using UnityEngine;
using System.Collections;

namespace DiceGame.UI.Effects
{
    public class CameraShake : MonoBehaviour
    {
        private Vector3 _originalPos;
        private Coroutine _shakeRoutine;

        private void Awake()
        {
            // Speichert die Ursprungsposition der Kamera
            _originalPos = transform.localPosition;
        }

        /// <summary>
        /// Startet ein Kamera-Wackeln.
        /// </summary>
        /// <param name="duration">Dauer in Sekunden</param>
        /// <param name="magnitude">Stärke des Wackelns</param>
        public void Shake(float duration, float magnitude)
        {
            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
            }
            _shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
        }

        private IEnumerator ShakeRoutine(float duration, float magnitude)
        {
            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                // Zufälliger Offset basierend auf der Magnitude
                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;

                transform.localPosition = new Vector3(x, y, _originalPos.z);

                elapsed += Time.deltaTime;
                yield return null; // Warte bis zum nächsten Frame
            }

            // Exakt auf die Ursprungsposition zurücksetzen
            transform.localPosition = _originalPos;
        }
    }
}