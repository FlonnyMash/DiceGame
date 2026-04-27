using UnityEngine;
using TMPro;

namespace DiceGame.UI.Views
{
    public class LeaderboardEntry : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _rankText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _scoreText;

        public void SetData(int rank, string name, int score)
        {
            _rankText.text = $"{rank}.";
            _nameText.text = name;
            _scoreText.text = $"{score} pts";
        }
    }
}