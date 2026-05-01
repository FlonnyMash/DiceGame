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
        
        // --- Die neuen, sauberen Animator-Befehle ---
        private const string ANIM_BOOL_READY = "IsReady";        
        private const string ANIM_TRIGGER_ADD = "OnPointsAdded"; 
        private const string ANIM_TRIGGER_CLAIMED = "OnClaimed"; 

        [Header("Fill Settings")]
        [SerializeField] private float _fillDuration = 0.5f;

        public event Action OnClaimClicked; 

        private int _targetScore = 63;
        private int _previousRawScore = 0; 

        private Coroutine _fillCoroutine;

        private bool _isClaimable = false;
        private bool _isClaimed = false;

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
            _previousRawScore = 0; 
            _isClaimable = false;
            _isClaimed = false;
            
            if (_claimButton != null) _claimButton.interactable = false;
            _bonusText.text = $"Bonus 0/{_targetScore}";
            if (_fillImage != null) _fillImage.fillAmount = 0f;
            
            if (_animator != null) _animator.SetBool(ANIM_BOOL_READY, false);
        }

        public void UpdateBonusState(int currentScore, bool isClaimed)
        {
            bool wasClaimedBefore = _isClaimed; 
            _isClaimed = isClaimed;
            
            int clampedScore = Mathf.Clamp(currentScore, 0, _targetScore);
            bool isNowReady = clampedScore >= _targetScore && !_isClaimed;

            // 1. Kleine Punkte-Hinzugefügt-Animation
            if (clampedScore > _previousRawScore && !isNowReady && !_isClaimed)
            {
                if (_animator != null) _animator.SetTrigger(ANIM_TRIGGER_ADD);
            }
            _previousRawScore = clampedScore;

            // 2. Große Claim-Animation 
            if (_isClaimed && !wasClaimedBefore)
            {
                if (_animator != null) _animator.SetTrigger(ANIM_TRIGGER_CLAIMED);
            }

            // 3. Dauerhaften Wackel-Status sichern
            if (_animator != null)
            {
                _animator.SetBool(ANIM_BOOL_READY, isNowReady);
            }

            // --- UI Texte & Buttons synchronisieren ---
            if (_isClaimed)
            {
                _bonusText.text = "Claimed!"; // Zurückgeändert auf deinen alten Text!
                if (_fillImage != null) _fillImage.fillAmount = 1f;
                if (_claimButton != null) _claimButton.interactable = false;
                return;
            }

            if (isNowReady)
            {
                _isClaimable = true;
                _bonusText.text = "CLAIM BONUS!";
                if (_fillImage != null) _fillImage.fillAmount = 1f;
                if (_claimButton != null) _claimButton.interactable = true;
                return;
            }

            _isClaimable = false;
            if (_claimButton != null) _claimButton.interactable = false;
            _bonusText.text = $"Bonus {clampedScore}/{_targetScore}";

            // --- DEINE FUNKTIONIERENDE FILL-LOGIK ---
            float targetFill = (float)clampedScore / _targetScore;
            
            if (_fillCoroutine != null) 
            {
                StopCoroutine(_fillCoroutine);
            }
            
            // Wichtiges Unity-Detail: Coroutines dürfen nur starten, wenn das GameObject aktiv ist.
            // Beim Spielerwechsel schaltet sich das UI manchmal kurz ab.
            if (gameObject.activeInHierarchy)
            {
                _fillCoroutine = StartCoroutine(AnimateFill(targetFill));
            }
            else if (_fillImage != null)
            {
                _fillImage.fillAmount = targetFill; // Springt sofort auf den Zielwert, wenn UI unsichtbar ist
            }
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
        
        private void OnDestroy()
        {
            if (_claimButton != null) _claimButton.onClick.RemoveAllListeners();
        }
    }
}