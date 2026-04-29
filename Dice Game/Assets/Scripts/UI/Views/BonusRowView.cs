using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace DiceGame.UI.Views
{
    public class BonusRowView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _bonusText;
        [SerializeField] private Image _fillImage;
        
        [Header("Animation Settings")]
        [SerializeField] private float _animationDuration = 0.5f; // Wie lange die Füll-Animation dauert

        private int _targetScore = 63; // Das Ziel für den Kniffel-Bonus
        private Coroutine _fillCoroutine;

        public void Initialize(int targetScore = 63)
        {
            _targetScore = targetScore;
            _bonusText.text = $"0/{_targetScore}";
            if (_fillImage != null) _fillImage.fillAmount = 0f;
        }

        // Wird von der ScoreCardView aufgerufen, wenn sich Punkte ändern
        public void UpdateBonusProgress(int currentScore)
        {
            // Verhindert, dass der Balken über das Ziel hinauswächst
            int clampedScore = Mathf.Clamp(currentScore, 0, _targetScore);
            _bonusText.text = $"{clampedScore}/{_targetScore}";

            // Berechnet den prozentualen Füllstand (0.0 bis 1.0)
            float targetFill = (float)clampedScore / _targetScore;

            // Startet die sanfte Animation
            if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
            _fillCoroutine = StartCoroutine(AnimateFill(targetFill));
        }

        private IEnumerator AnimateFill(float targetFill)
        {
            if (_fillImage == null) yield break;

            float startFill = _fillImage.fillAmount;
            float elapsedTime = 0f;

            while (elapsedTime < _animationDuration)
            {
                elapsedTime += Time.deltaTime;
                // Interpoliert weich zwischen dem alten und neuen Füllstand
                _fillImage.fillAmount = Mathf.Lerp(startFill, targetFill, elapsedTime / _animationDuration);
                yield return null; // Wartet einen Frame
            }

            _fillImage.fillAmount = targetFill; // Zur Sicherheit am Ende exakt setzen
        }
    }
}