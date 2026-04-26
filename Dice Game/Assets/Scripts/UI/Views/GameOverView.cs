using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DiceGame.UI.Views
{
    public class GameOverView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _finalScoreText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton; // Main Menu Button

        public event Action OnRestartClicked;
        public event Action OnMainMenuClicked; // NEU: Event für das Main Menu

        private void Awake()
        {
            _restartButton.onClick.AddListener(() => OnRestartClicked?.Invoke());
            _mainMenuButton.onClick.AddListener(() => OnMainMenuClicked?.Invoke()); // Event für das Main Menu
        }

        public void Show(int finalScore)
        {
            _finalScoreText.text = $"Score: {finalScore}";
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_restartButton != null) _restartButton.onClick.RemoveAllListeners();
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveAllListeners();
        }
    }
}