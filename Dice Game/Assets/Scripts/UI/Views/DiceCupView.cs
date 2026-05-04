using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using DiceGame.Services; 

namespace DiceGame.UI.Views
{
    [RequireComponent(typeof(CanvasGroup))]
    public class DiceCupView : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("UI References")]
        [SerializeField] private RectTransform _cupImageRect; 
        
        [Header("Shake Constraints")]
        [Tooltip("Ein unsichtbares UI-Objekt irgendwo im Canvas, das den globalen Bereich definiert.")]
        [SerializeField] private RectTransform _shakeAreaBounds; 
        [SerializeField] private float _shakeIntensity = 15f; 

        public RectTransform CupImageRect => _cupImageRect;

        public event Action OnCupTouched; 
        public event Action OnCupDragged; 
        public event Action OnShakeCompleted;

        private CanvasGroup _canvasGroup;
        private bool _isInteractable = false;
        private bool _isShaking = false;

        private float _lastVibrateTime = 0f;
        private const float VIBRATE_COOLDOWN = 0.15f; 

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            
            if (_cupImageRect != null) _cupImageRect.anchoredPosition = Vector2.zero;
        }

        public void EnableInteraction()
        {
            _isInteractable = true;
            _canvasGroup.blocksRaycasts = true; 
        }

        // NEU: Methode zum expliziten Sperren durch den Controller
        public void DisableInteraction()
        {
            _isInteractable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!_isInteractable) return;
            _isShaking = true;
            
            OnCupTouched?.Invoke(); 
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isShaking || !_isInteractable || _shakeAreaBounds == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            _cupImageRect.anchoredPosition += eventData.delta / scaleFactor;

            Vector3[] corners = new Vector3[4];
            _shakeAreaBounds.GetWorldCorners(corners);
            
            Vector3 cupWorldPos = _cupImageRect.position;

            cupWorldPos.x = Mathf.Clamp(cupWorldPos.x, corners[0].x, corners[2].x);
            cupWorldPos.y = Mathf.Clamp(cupWorldPos.y, corners[0].y, corners[2].y);

            _cupImageRect.position = cupWorldPos;
            
            _cupImageRect.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity));

            if (Time.time - _lastVibrateTime > VIBRATE_COOLDOWN)
            {
                HapticService.PlayShakeHaptic();
                _lastVibrateTime = Time.time;
            }

            OnCupDragged?.Invoke(); 
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isShaking || !_isInteractable) return;
            
            _isShaking = false;
            // ENTFERNT: Der Becher sperrt sich hier NICHT mehr selbst! Er geht nur in die Mitte zurück.
            
            _cupImageRect.anchoredPosition = Vector2.zero;
            _cupImageRect.localEulerAngles = Vector3.zero;

            OnShakeCompleted?.Invoke();
        }

        public IEnumerator AnimateRevealRoutine()
        {
            float t = 0;
            Vector2 startPos = _cupImageRect.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(-400, 0f); 

            HapticService.PlayShakeHaptic();

            while (t < 0.4f)
            {
                t += Time.deltaTime;
                _cupImageRect.anchoredPosition = Vector2.Lerp(startPos, endPos, t / 0.4f);
                yield return null;
            }
            _cupImageRect.anchoredPosition = endPos;
        }

        public void ResetCup()
        {
            DisableInteraction();
            
            _cupImageRect.anchoredPosition = Vector2.zero;
            _cupImageRect.localEulerAngles = Vector3.zero;
        }

        public IEnumerator AutoShakeRoutine()
        {
            OnCupTouched?.Invoke();

            float t = 0;
            while (t < 1.0f) 
            {
                t += Time.deltaTime;
                _cupImageRect.anchoredPosition = UnityEngine.Random.insideUnitCircle * 200f;
                _cupImageRect.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-_shakeIntensity, _shakeIntensity));
                
                if (Time.time - _lastVibrateTime > VIBRATE_COOLDOWN)
                {
                    HapticService.PlayShakeHaptic();
                    _lastVibrateTime = Time.time;
                }

                yield return null;
            }
            
            _cupImageRect.anchoredPosition = Vector2.zero;
            _cupImageRect.localEulerAngles = Vector3.zero;
        }
    }
}