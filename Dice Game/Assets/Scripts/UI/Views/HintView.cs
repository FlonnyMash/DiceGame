using UnityEngine;
using TMPro;
using DiceGame.Core.Rules;
using DiceGame.Services;
using System.Collections;

namespace DiceGame.UI.Views
{
    [RequireComponent(typeof(CanvasGroup))]
    public class HintView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _hintText;
        [SerializeField] private Animator _animator;
        [SerializeField] private CanvasGroup _canvasGroup;

        // Wir nutzen Hashes statt Strings für bessere Performance und Sicherheit
        private static readonly int ShowHintHash = Animator.StringToHash("HintPopup_Show");
        private bool _isShowingThisTurn = false;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_animator == null) _animator = GetComponent<Animator>();
            
            // Initialzustand: Unsichtbar
            _canvasGroup.alpha = 0;
            transform.localScale = Vector3.zero;
        }

        public void ShowHint(ScoreCategory category)
        {
            if (_isShowingThisTurn) return;
            _isShowingThisTurn = true;

            // Lokalisierung laden
            string locKey = $"hint_{category.ToString().ToLower()}";
            _hintText.text = LocalizationService.Instance.GetText(locKey);

            gameObject.SetActive(true);

            StopAllCoroutines();
            StartCoroutine(PlayAnimationRoutine());
        }

        private IEnumerator PlayAnimationRoutine()
        {
            // Warten, bis das Objekt im Frame registriert ist
            yield return new WaitForEndOfFrame();
            
            if (_animator != null)
            {
                // Prüfen, ob der State auf Layer 0 existiert
                if (_animator.HasState(0, ShowHintHash))
                {
                    // Layer 0 explizit angeben (statt -1)
                    _animator.Play(ShowHintHash, 0, 0f);
                }
                else
                {
                    Debug.LogError($"[HintView] State 'HintPopup_Show' nicht im Animator von {gameObject.name} gefunden! Prüfe die Schreibweise im Animator-Fenster.");
                }
            }
        }

        public void HideHint()
        {
            _isShowingThisTurn = false;
            
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}