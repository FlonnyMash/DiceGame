using UnityEngine;
using DiceGame.Core.Models;

namespace DiceGame.Configs
{
    [CreateAssetMenu(fileName = "NewShopItem", menuName = "DiceGame/Shop Item")]
    public class ShopItemConfig : ScriptableObject
    {
        [Header("Core Data")]
        public string Id;
        public ShopItemType Type;
        public int Price;
        
        // NEU: Haken im Inspector / Tool
        [Tooltip("Wenn aktiv, hat jeder Spieler dieses Item von Anfang an kostenlos im Inventar.")]
        public bool IsDefaultItem;
        
        [Header("Localization Keys")]
        public string NameLocKey;
        public string DescLocKey;

        [Header("Visuals (Shop)")]
        [Tooltip("Das Icon, das im Shop UI (ShopView) angezeigt wird.")]
        public Sprite ShopIcon;
        
        [Header("In-Game Data")]
        [Tooltip("Zwingend erforderlich für den Typ 'DiceSkin'. Beinhaltet die 6 Würfelseiten.")]
        public DiceSkinConfig DiceSkin; 
    }
}