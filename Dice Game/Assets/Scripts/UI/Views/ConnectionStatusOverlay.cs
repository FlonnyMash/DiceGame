using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DiceGame.Core.Networking;
using DiceGame.Services;

namespace DiceGame.UI.Views
{
    // Localized status overlay used by both OnlineLobbyController (MainMenu) and GameController
    // (InGameScene). Re-translates on language change so a locale switch mid-overlay isn't stale.
    //
    // Show takes a localization key + optional format args, NOT a baked string. That keeps every
    // caller at the level of "this is the situation" instead of "this is the literal sentence".
    public class ConnectionStatusOverlay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _label;
        [SerializeField] private Button _backToMenuButton;

        [Header("Behaviour")]
        [SerializeField] private bool _hideOnAwake = true;

        public event Action OnBackToMenuClicked;

        private string _currentKey;
        private object[] _currentArgs;
        private bool _backWired;

        private void Awake()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_hideOnAwake) Hide();

            if (_backToMenuButton != null && !_backWired)
            {
                _backToMenuButton.onClick.AddListener(HandleBackClicked);
                _backWired = true;
            }
        }

        private void OnEnable()
        {
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged += RefreshLabel;
            }
        }

        private void OnDisable()
        {
            if (LocalizationService.Instance != null)
            {
                LocalizationService.Instance.OnLanguageChanged -= RefreshLabel;
            }
        }

        private void OnDestroy()
        {
            if (_backToMenuButton != null && _backWired)
            {
                _backToMenuButton.onClick.RemoveListener(HandleBackClicked);
                _backWired = false;
            }
        }

        public void Show(string locKey, params object[] args)
        {
            _currentKey = locKey;
            _currentArgs = args;
            RefreshLabel();
            ApplyVisibility(true, showBackButton: false);
        }

        // Used for terminal error states (e.g. err_connection_lost, err_desync_detected) where the
        // user must manually return to the main menu. The match-abort flow drives this entry point.
        public void ShowTerminal(string locKey, params object[] args)
        {
            _currentKey = locKey;
            _currentArgs = args;
            RefreshLabel();
            ApplyVisibility(true, showBackButton: true);
        }

        public void Hide()
        {
            _currentKey = null;
            _currentArgs = null;
            ApplyVisibility(false, showBackButton: false);
        }

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0.5f && gameObject.activeSelf;

        private void RefreshLabel()
        {
            if (_label == null || string.IsNullOrEmpty(_currentKey)) return;
            string text = LocalizationService.Instance != null
                ? LocalizationService.Instance.GetText(_currentKey, _currentArgs)
                : _currentKey;
            _label.text = text;
        }

        private void ApplyVisibility(bool visible, bool showBackButton)
        {
            gameObject.SetActive(visible || _canvasGroup == null);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = visible ? 1f : 0f;
                _canvasGroup.blocksRaycasts = visible;
                _canvasGroup.interactable = visible;
            }
            if (_backToMenuButton != null)
            {
                _backToMenuButton.gameObject.SetActive(visible && showBackButton);
            }
        }

        private void HandleBackClicked() => OnBackToMenuClicked?.Invoke();

        // Convenience helper used by callers that subscribe to NetworkSessionDirector or the raw
        // transport. Centralised here so the locKey mapping has one home.
        public void ApplyStatus(NetworkStatus status, bool hasBeenConnected)
        {
            switch (status)
            {
                case NetworkStatus.Connecting:
                    Show("msg_connecting");
                    break;
                case NetworkStatus.Reconnecting:
                    Show("msg_connecting");
                    break;
                case NetworkStatus.Connected:
                    Hide();
                    break;
                case NetworkStatus.Disconnected:
                    if (hasBeenConnected) ShowTerminal("err_connection_lost");
                    break;
                case NetworkStatus.Error:
                    ShowTerminal("err_connection_lost");
                    break;
            }
        }
    }
}
