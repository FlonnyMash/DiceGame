using UnityEngine;

namespace DiceGame.Configs
{
    [CreateAssetMenu(fileName = "NewDiceSkin", menuName = "DiceGame/Dice Skin Config")]
    public class DiceSkinConfig : ScriptableObject
    {
        [Tooltip("Die ID muss exakt mit der ID im ShopItemConfig übereinstimmen! (z.B. 'dice_neon')")]
        public string Id;

        [Tooltip("Die 6 Seiten des Würfels. Index 0 = Wert 1, Index 5 = Wert 6")]
        public Sprite[] Faces = new Sprite[6];
        
        // Optional für später:
        // public Material MaterialOverride;
        // public GameObject ParticleEffectOnRoll;
    }
}