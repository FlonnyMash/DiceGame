using UnityEngine;
using TMPro;
using Unity.VectorGraphics; // <-- NEU: Gibt uns Zugriff auf das SVGImage

public class PlayerScoreEntry : MonoBehaviour
{
    // HIER: Das Skript fragt jetzt nach der SVG-Komponente
    [SerializeField] private SVGImage _rankImage; 
    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private TextMeshProUGUI _scoreText;

    public void SetData(Sprite rankSprite, string playerName, int score)
    {
        _playerNameText.text = playerName;
        _scoreText.text = score.ToString();

        if (rankSprite != null)
        {
            // Ein SVGImage akzeptiert Vector Sprites als Quelle
            _rankImage.sprite = rankSprite;
            _rankImage.gameObject.SetActive(true);
        }
        else
        {
            _rankImage.gameObject.SetActive(false);
        }
    }
}