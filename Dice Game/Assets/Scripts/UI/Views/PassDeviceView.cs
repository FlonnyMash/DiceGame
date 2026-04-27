using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceGame.UI.Views
{
    public class PassDeviceView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _readyButton;

        // Sagt dem Controller: "Der nächste Spieler hat das Handy und ist bereit!"
        public event Action OnReadyClicked;

        private void Awake()
        {
            if (_readyButton != null)
            {
                _readyButton.onClick.AddListener(() => OnReadyClicked?.Invoke());
            }
        }

        public void Show(string nextPlayerName)
        {
            // Text setzen und Panel einblenden
            _messageText.text = $"{nextPlayerName} is next!\nPass the device.";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_readyButton != null) _readyButton.onClick.RemoveAllListeners();
        }
    }
}