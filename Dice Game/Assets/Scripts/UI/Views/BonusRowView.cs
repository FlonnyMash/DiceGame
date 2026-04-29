using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceGame.UI.Views
{
    public class BonusRowView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _bonusText;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Button _claimButton; 

        [Header("Animation (Unity Animator)")]
        [SerializeField] private Animator _animator; 
        private const string ANIM_TRIGGER_READY = "OnReady"; 
        private const string ANIM_TRIGGER_STOP = "OnStop";
        private const string ANIM_TRIGGER_ADD = "OnPointsAdded"; // <--- NEU

        [Header("Fill Settings")]
        [SerializeField] private float _fillDuration = 0.5f;

        public event Action OnClaimClicked; 

        private int _targetScore = 63;
        private int _previousRawScore = 0; // <--- NEU: Merkt sich den alten Stand
        private Coroutine _fillCoroutine;

        private bool _isClaimable = false;
        private bool _isClaimed = false;
        private bool _isReadyAnimating = false; 

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_claimButton == null) _claimButton = GetComponent<Button>();
            if (_claimButton != null)
            {
                _claimButton.onClick.AddListener(() => 
                {
                    if (_isClaimable && !_isClaimed) OnClaimClicked?.Invoke();
                });
            }
        }

        public void Initialize(int targetScore = 63)
        {
            _targetScore = targetScore;
            _previousRawScore = 0; // <--- NEU: Reset
            
            if (_claimButton != null) _claimButton.interactable = false;
            
            _bonusText.text = $"Bonus 0/{_targetScore}";
            if (_fillImage != null) _fillImage.fillAmount = 0f;
            
            StopReadyAnimation();
        }

        public void UpdateBonusState(int currentScore, bool isClaimed)
        {
            _isClaimed = isClaimed;
            int clampedScore = Mathf.Clamp(currentScore, 0, _targetScore);

            bool isNowReady = clampedScore >= _targetScore;

            // --- KORREKTUR: Trigger-Konflikt verhindern ---
            // Nur die kleine Animation spielen, wenn wir die 63 noch NICHT erreicht haben!
            if (clampedScore > _previousRawScore && !_isClaimable && !_isClaimed && !isNowReady)
            {
                TriggerPointsAddedAnimation();
            }
            _previousRawScore = clampedScore; 
            // ----------------------------------------------

            // 1. STATUS: BEREITS EINGESAMMELT
            if (_isClaimed)
            {
                StopReadyAnimation();
                _bonusText.text = "BONUS: 35"; 
                if (_fillImage != null) _fillImage.fillAmount = 1f;
                if (_claimButton != null) _claimButton.interactable = false;
                return;
            }

            // 2. STATUS: BEREIT ZUM EINSAMMELN
            if (isNowReady)
            {
                _isClaimable = true;
                _bonusText.text = "CLAIM BONUS!";
                if (_fillImage != null) _fillImage.fillAmount = 1f;
                if (_claimButton != null) _claimButton.interactable = true;
                StartReadyAnimation();
                return;
            }

            // 3. STATUS: NOCH NICHT ERREICHT
            _isClaimable = false;
            StopReadyAnimation();
            if (_claimButton != null) _claimButton.interactable = false;
            _bonusText.text = $"Bonus {clampedScore}/{_targetScore}";

            float targetFill = (float)clampedScore / _targetScore;
            if (_fillCoroutine != null) StopCoroutine(_fillCoroutine);
            _fillCoroutine = StartCoroutine(AnimateFill(targetFill));
        }

        private IEnumerator AnimateFill(float targetFill)
        {
            if (_fillImage == null) yield break;
            float startFill = _fillImage.fillAmount;
            float elapsedTime = 0f;
            while (elapsedTime < _fillDuration)
            {
                elapsedTime += Time.deltaTime;
                _fillImage.fillAmount = Mathf.Lerp(startFill, targetFill, elapsedTime / _fillDuration);
                yield return null;
            }
            _fillImage.fillAmount = targetFill;
        }

        // --- NEU: Die kleine Feedback-Animation ---
        private void TriggerPointsAddedAnimation()
        {
            if (_animator != null)
            {
                // Wir nutzen SetTrigger, damit die Transition im Animator anspringt
                _animator.SetTrigger(ANIM_TRIGGER_ADD);
            }
        }

        private void StartReadyAnimation()
        {
            if (_animator != null && !_isReadyAnimating)
            {
                _isReadyAnimating = true;
                _animator.SetTrigger(ANIM_TRIGGER_READY);
            }
        }

        private void StopReadyAnimation()
        {
            if (_animator != null)
            {
                _isReadyAnimating = false;
                _animator.SetTrigger(ANIM_TRIGGER_STOP);
            }
        }
        
        private void OnDestroy()
        {
            if (_claimButton != null) _claimButton.onClick.RemoveAllListeners();
        }
    }
}