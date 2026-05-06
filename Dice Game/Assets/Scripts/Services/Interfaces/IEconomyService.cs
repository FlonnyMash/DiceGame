using System;
using System.Collections.Generic;
using DiceGame.Core.Models;

namespace DiceGame.Services.Interfaces
{
    public interface IEconomyService
    {
        Wallet PlayerWallet { get; }
        Inventory PlayerInventory { get; }
        
        // NEU: Loadout-Status
        string EquippedDiceId { get; }
        string EquippedCupId { get; }
        event Action OnLoadoutChanged;

        IEnumerable<ShopItem> GetAvailableShopItems();
        bool PurchaseItem(ShopItem item);
        void EquipItem(ShopItem item); // NEU: Item ausrüsten

        void SaveEconomy();
        void LoadEconomy();
    }
}