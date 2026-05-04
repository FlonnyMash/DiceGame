using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections;

namespace DiceGame.UI.Views
{
    public class DieView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI References")]
        [SerializeField] private Image _dieImage;
        [SerializeField] private GameObject _heldHighlight;
        [SerializeField] private GameObject _heldBoarder;

        [Header("Dice Faces (1 to 6)")]
        [SerializeField] private Sprite[] _diceFaces;

        [Header("Animation & Transform")]
        [SerializeField] private Animator _animator;
        [SerializeField] private float _moveSpeed = 0.25f;
        [SerializeField] private float _scatterScale = 0.7f;

        private const string ANIM_TRIGGER_SELECT = "OnSelect";
        private const string ANIM_TRIGGER_DESELECT = "OnDeselect";

        public event Action<int> OnDieClicked;

        private int _dieIndex;
        private bool _isHeld;

        private Vector2 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialScale; 
        
        private Vector2 _scatteredPosition;
        private Quaternion _scatteredRotation;

        private Coroutine _moveRoutine;

        public RectTransform Rect => GetComponent<RectTransform>();
        public Vector2 InitialPosition => _initialPosition;
        public Vector2 ScatteredPosition => _scatteredPosition;

        #if DEVELOPMENT_BUILD || UNITY_EDITOR

        /// <summary>
        /// Nur für den Unity Editor oder Development Builds: Zwingt den Würfel auf einen bestimmten Wert, 
        /// um seltene Kombinationen (wie Nicer Dicer) schnell zu testen.
        /// </summary>
        public void DebugForceValue(int value)
        {
            Value = value;
        }
#endif

        // NEU: Rechnet die aktuelle Scattered Position in World Space um (Trick!)
        public Vector3 ScatteredWorldPosition
        {
            get
            {
                RectTransform rect = GetComponent<RectTransform>();
                Vector2 currentAnchored = rect.anchoredPosition;
                rect.anchoredPosition = _scatteredPosition;
                Vector3 worldPos = rect.position;
                rect.anchoredPosition = currentAnchored;
                return worldPos;
            }
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        public void Initialize(int index)
        {
            _dieIndex = index;
            
            RectTransform rect = GetComponent<RectTransform>();
            _initialPosition = rect.anchoredPosition;
            _initialRotation = rect.localRotation;
            _initialScale = rect.localScale;
            
            _scatteredPosition = _initialPosition;
            _scatteredRotation = _initialRotation;
        }

        public void UpdateView(int value, bool isHeld)
        {
            _isHeld = isHeld;

            if (value >= 1 && value <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[value - 1];
            }

            if (_heldHighlight != null) _heldHighlight.SetActive(isHeld);
            if (_heldBoarder != null) _heldBoarder.SetActive(isHeld);
        }

        public void SetVisibility(bool isVisible)
        {
            if (_dieImage != null) _dieImage.enabled = isVisible;
            if (_heldHighlight != null) _heldHighlight.SetActive(isVisible && _isHeld);
            if (_heldBoarder != null) _heldBoarder.SetActive(isVisible && _isHeld);
        }

        // NEU: Akzeptiert jetzt eine World Position!
        public void SetScatterTargetWorld(Vector3 targetWorldPos, float randomZRotation)
        {
            RectTransform rect = GetComponent<RectTransform>();
            Vector2 oldPos = rect.anchoredPosition;
            
            // Setze ihn kurz auf die Weltposition, speichere den lokalen Wert, und setze ihn zurück
            rect.position = targetWorldPos;
            _scatteredPosition = rect.anchoredPosition;
            rect.anchoredPosition = oldPos; 
            
            _scatteredRotation = Quaternion.Euler(0, 0, randomZRotation);
        }

        // NEU: Akzeptiert jetzt eine World Position!
        public void ScatterWorld(Vector3 targetWorldPos, float randomZRotation)
        {
            if (_isHeld) return;

            SetScatterTargetWorld(targetWorldPos, randomZRotation);

            RectTransform rect = GetComponent<RectTransform>();
            rect.anchoredPosition = _scatteredPosition;
            rect.localRotation = _scatteredRotation;
            rect.localScale = _initialScale * _scatterScale;
        }

        public void AnimateToState(bool isHeld)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            
            Vector2 targetPos = isHeld ? _initialPosition : _scatteredPosition;
            Quaternion targetRot = isHeld ? _initialRotation : _scatteredRotation;
            Vector3 targetScale = isHeld ? _initialScale : _initialScale * _scatterScale;
            
            _moveRoutine = StartCoroutine(MoveRoutine(targetPos, targetRot, targetScale, _moveSpeed));
        }

        private IEnumerator MoveRoutine(Vector2 targetPos, Quaternion targetRot, Vector3 targetScale, float duration)
        {
            RectTransform rect = GetComponent<RectTransform>();
            Vector2 startPos = rect.anchoredPosition;
            Quaternion startRot = rect.localRotation;
            Vector3 startScale = rect.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); 
                
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                rect.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                rect.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            rect.anchoredPosition = targetPos;
            rect.localRotation = targetRot;
            rect.localScale = targetScale;
        }

        public Coroutine SlideToPosition(Vector2 targetPos, float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(SlideToPositionRoutine(targetPos, duration));
            return _moveRoutine;
        }

        private IEnumerator SlideToPositionRoutine(Vector2 targetPos, float duration)
        {
            RectTransform rect = GetComponent<RectTransform>();
            Vector2 startPos = rect.anchoredPosition;
            Vector3 startScale = rect.localScale;
            Vector3 targetScale = _initialScale * _scatterScale; 
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                rect.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }
            rect.anchoredPosition = targetPos;
            rect.localScale = targetScale;
        }

        public Coroutine SlideBackToTray(float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(SlideBackToTrayRoutine(duration));
            return _moveRoutine;
        }

        private IEnumerator SlideBackToTrayRoutine(float duration)
        {
            RectTransform rect = GetComponent<RectTransform>();
            Vector2 startPos = rect.anchoredPosition;
            Quaternion startRot = rect.localRotation;
            Vector3 startScale = rect.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                rect.anchoredPosition = Vector2.Lerp(startPos, _initialPosition, t);
                rect.localRotation = Quaternion.Lerp(startRot, _initialRotation, t);
                rect.localScale = Vector3.Lerp(startScale, _initialScale, t); 
                yield return null;
            }

            rect.anchoredPosition = _initialPosition;
            rect.localRotation = _initialRotation;
            rect.localScale = _initialScale;
            _scatteredPosition = _initialPosition;
            _scatteredRotation = _initialRotation;
        }

        public void PlayToggleAnimation(bool isNowHeld)
        {
            if (_animator == null) return;

            if (isNowHeld)
            {
                _animator.ResetTrigger(ANIM_TRIGGER_DESELECT);
                _animator.SetTrigger(ANIM_TRIGGER_SELECT);
            }
            else
            {
                _animator.ResetTrigger(ANIM_TRIGGER_SELECT);
                _animator.SetTrigger(ANIM_TRIGGER_DESELECT);
            }
        }

        public void AnimateReset()
        {
            _isHeld = false;
            SetVisibility(true); 

            if (_heldHighlight != null) _heldHighlight.SetActive(false);
            if (_heldBoarder != null) _heldBoarder.SetActive(false);

            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }

            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(MoveRoutine(_initialPosition, _initialRotation, _initialScale, _moveSpeed));

            _scatteredPosition = _initialPosition;
            _scatteredRotation = _initialRotation;
        }

        public void ResetToIdleSilent()
        {
            _isHeld = false;
            SetVisibility(true); 

            if (_heldHighlight != null) _heldHighlight.SetActive(false);
            if (_heldBoarder != null) _heldBoarder.SetActive(false);

            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            RectTransform rect = GetComponent<RectTransform>();
            rect.localScale = _initialScale;
            rect.anchoredPosition = _initialPosition;
            rect.localRotation = _initialRotation;
            
            _scatteredPosition = _initialPosition;
            _scatteredRotation = _initialRotation;

            if (_animator != null)
            {
                _animator.Rebind();
                _animator.Update(0f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnDieClicked?.Invoke(_dieIndex);
        }
    }
}