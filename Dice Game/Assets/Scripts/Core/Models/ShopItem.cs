namespace DiceGame.Core.Models
{
    public class ShopItem
    {
        public string Id { get; private set; }
        public ShopItemType Type { get; private set; }
        public int Price { get; private set; }
        
        public string NameLocKey { get; private set; } 
        public string DescLocKey { get; private set; }
        
        // NEU: Flag für Standard-Items
        public bool IsDefaultItem { get; private set; }

        public ShopItem(string id, ShopItemType type, int price, string nameLocKey, string descLocKey, bool isDefaultItem)
        {
            Id = id;
            Type = type;
            Price = price;
            NameLocKey = nameLocKey;
            DescLocKey = descLocKey;
            IsDefaultItem = isDefaultItem;
        }
    }
}