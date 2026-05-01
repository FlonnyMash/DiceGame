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

        [Header("Animation")]
        [SerializeField] private Animator _animator;
        
        // Wir nutzen jetzt Trigger für präzise Steuerung
        private const string ANIM_TRIGGER_SELECT = "OnSelect"; 
        private const string ANIM_TRIGGER_DESELECT = "OnDeselect"; 
        private const string ANIM_TRIGGER_RESET = "SilentReset"; 

        public event Action<int> OnDieClicked; 

        private int _dieIndex;
        private bool _isHeld; 

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
        }

        public void Initialize(int index)
        {
            _dieIndex = index;
        }

        // Kümmert sich NUR noch um das Aussehen (Bilder/Rahmen), NICHT mehr um Animationen!
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

        // --- NEU: Explizite Animations-Steuerung ---
        
        // Wird beim Klicken (oder durch den Presenter) aufgerufen
        public void PlayToggleAnimation(bool isNowHeld)
        {
            if (_animator == null) return;

            if (isNowHeld)
            {
                // 1. Zuerst den alten/falschen Trigger löschen!
                _animator.ResetTrigger(ANIM_TRIGGER_DESELECT);
                // 2. Dann den neuen setzen
                _animator.SetTrigger(ANIM_TRIGGER_SELECT);
            }
            else
            {
                // 1. Zuerst den alten/falschen Trigger löschen!
                _animator.ResetTrigger(ANIM_TRIGGER_SELECT);
                // 2. Dann den neuen setzen
                _animator.SetTrigger(ANIM_TRIGGER_DESELECT);
            }
        }

        // Wird vom Controller aufgerufen, wenn der Turn wechselt (ohne Animation!)
        public void ResetToIdleSilent()
        {
            _isHeld = false; 

            // Visuelles hartes Reset
            if (_heldHighlight != null) _heldHighlight.SetActive(false);
            if (_heldBoarder != null) _heldBoarder.SetActive(false);
            transform.localScale = Vector3.one;

            if (_animator != null)
            {
                // DIE NUKLEARE OPTION: 
                // Rebind() löscht sofort alle aktiven Trigger, bricht alle laufenden 
                // Transitionen ab und setzt den Animator knallhart auf den Default-State 
                // (Idle) des Layer 0 zurück. Keine Geister-Trigger können das überleben!
                _animator.Rebind();
                
                // Zwingt Unity, dieses Rebind noch im exakt selben Frame zu berechnen
                _animator.Update(0f);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnDieClicked?.Invoke(_dieIndex);
        }

        public void AnimateRoll(int finalValue, float duration)
        {
            if (_isHeld) return; 
            StartCoroutine(RollAnimationRoutine(finalValue, duration));
        }

        private IEnumerator RollAnimationRoutine(int finalValue, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (_diceFaces.Length > 0)
                {
                    int randomFace = UnityEngine.Random.Range(0, _diceFaces.Length);
                    _dieImage.sprite = _diceFaces[randomFace];
                }
                yield return new WaitForSeconds(0.05f);
                elapsed += 0.05f;
            }

            if (finalValue >= 1 && finalValue <= 6 && _diceFaces.Length == 6)
            {
                _dieImage.sprite = _diceFaces[finalValue - 1];
            }
        }
    }
}