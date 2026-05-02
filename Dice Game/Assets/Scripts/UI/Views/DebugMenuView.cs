using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.UI.Views
{
    public class DebugMenuView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _debugPanel;
        [SerializeField] private Button _secretTriggerButton; 
        [SerializeField] private Button _closeButton;
        
        [Header("Cheat Buttons")]
        [SerializeField] private Button _forceSixesButton;
        [SerializeField] private Button _forceStraightButton; 
        [SerializeField] private Button _resetBoardButton;
        [SerializeField] private Button _forceBonusButton; // NEU: Der Bonus Button

        public event Action OnForceSixesClicked;
        public event Action OnForceStraightClicked;
        public event Action OnResetBoardClicked;
        public event Action OnForceBonusClicked; // NEU: Das Event für den GameController

        private int _tapCount = 0;
        private float _lastTapTime = 0f;
        private const float TAP_TIMEOUT = 0.5f; 

        private void Awake()
        {
            if (_debugPanel != null) _debugPanel.SetActive(false);

            if (_secretTriggerButton != null)
                _secretTriggerButton.onClick.AddListener(HandleSecretTap);

            if (_closeButton != null)
                _closeButton.onClick.AddListener(() => _debugPanel.SetActive(false));

            if (_forceSixesButton != null)
                _forceSixesButton.onClick.AddListener(() => OnForceSixesClicked?.Invoke());

            if (_forceStraightButton != null)
                _forceStraightButton.onClick.AddListener(() => OnForceStraightClicked?.Invoke());

            if (_resetBoardButton != null)
                _resetBoardButton.onClick.AddListener(() => OnResetBoardClicked?.Invoke());

            // NEU: Listener für den Bonus Cheat
            if (_forceBonusButton != null)
                _forceBonusButton.onClick.AddListener(() => OnForceBonusClicked?.Invoke());
        }

        private void HandleSecretTap()
        {
            if (Time.time - _lastTapTime > TAP_TIMEOUT)
            {
                _tapCount = 0;
            }

            _lastTapTime = Time.time;
            _tapCount++;

            if (_tapCount >= 5)
            {
                if (_debugPanel != null) _debugPanel.SetActive(true);
                _tapCount = 0;
            }
        }
    }
}