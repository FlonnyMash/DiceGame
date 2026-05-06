using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;
using DiceGame.Configs; 

namespace DiceGame.Services
{
    public class PlayerPrefsEconomyService : IEconomyService
    {
        private static PlayerPrefsEconomyService _instance;
        public static PlayerPrefsEconomyService Instance => _instance ??= new PlayerPrefsEconomyService();

        public Wallet PlayerWallet { get; private set; }
        public Inventory PlayerInventory { get; private set; }
        
        public string EquippedDiceId { get; private set; }
        public string EquippedCupId { get; private set; }
        public event Action OnLoadoutChanged;

        private List<ShopItem> _catalog;

        private const string PREF_COINS = "Wallet_Coins";
        private const string PREF_INVENTORY = "Inventory_Unlocked";
        private const string PREF_EQUIPPED_DICE = "Loadout_Dice";
        private const string PREF_EQUIPPED_CUP = "Loadout_Cup";

        public PlayerPrefsEconomyService()
        {
            InitializeCatalog();
            LoadEconomy();

            PlayerWallet.OnCoinsChanged += _ => SaveEconomy();
            PlayerInventory.OnItemUnlocked += _ => SaveEconomy();
        }

        private void InitializeCatalog()
        {
            _catalog = new List<ShopItem>();
            ShopItemConfig[] configs = Resources.LoadAll<ShopItemConfig>("ShopItems");
            
            foreach (var config in configs)
            {
                // IsDefaultItem wird hier übergeben
                _catalog.Add(new ShopItem(
                    config.Id, 
                    config.Type, 
                    config.Price, 
                    config.NameLocKey, 
                    config.DescLocKey,
                    config.IsDefaultItem
                ));
            }
        }

        public IEnumerable<ShopItem> GetAvailableShopItems() => _catalog;

        public bool PurchaseItem(ShopItem item)
        {
            if (PlayerInventory.HasItem(item.Id)) return false; 
            if (item.Price > 0 && !PlayerWallet.SpendCoins(item.Price)) return false; 

            PlayerInventory.UnlockItem(item.Id);
            return true;
        }

        public void EquipItem(ShopItem item)
        {
            if (!PlayerInventory.HasItem(item.Id)) return;

            if (item.Type == ShopItemType.DiceSkin) EquippedDiceId = item.Id;
            else if (item.Type == ShopItemType.CupSkin) EquippedCupId = item.Id;
            
            SaveEconomy();
            OnLoadoutChanged?.Invoke();
        }

        public void LoadEconomy()
        {
            int coins = PlayerPrefs.GetInt(PREF_COINS, 0);
            PlayerWallet = new Wallet(coins);

            string inventoryJson = PlayerPrefs.GetString(PREF_INVENTORY, "[]");
            var unlockedIds = JsonConvert.DeserializeObject<List<string>>(inventoryJson) ?? new List<string>();
            
            // NEU: Dynamisch alle Default-Items freischalten!
            string fallbackDiceId = "";
            foreach (var item in _catalog)
            {
                if (item.IsDefaultItem)
                {
                    if (!unlockedIds.Contains(item.Id)) unlockedIds.Add(item.Id);
                    
                    // Wir merken uns die ID des Default-Würfels als Fallback
                    if (item.Type == ShopItemType.DiceSkin && string.IsNullOrEmpty(fallbackDiceId))
                    {
                        fallbackDiceId = item.Id;
                    }
                }
            }

            PlayerInventory = new Inventory(unlockedIds);

            // Ausrüstung laden (Fallback auf das dynamisch gefundene Default Item)
            EquippedDiceId = PlayerPrefs.GetString(PREF_EQUIPPED_DICE, fallbackDiceId);
            EquippedCupId = PlayerPrefs.GetString(PREF_EQUIPPED_CUP, "");
        }

        public void SaveEconomy()
        {
            PlayerPrefs.SetInt(PREF_COINS, PlayerWallet.Coins);
            
            var unlockedIds = new List<string>(PlayerInventory.GetAllUnlockedItems());
            string inventoryJson = JsonConvert.SerializeObject(unlockedIds);
            PlayerPrefs.SetString(PREF_INVENTORY, inventoryJson);
            
            PlayerPrefs.SetString(PREF_EQUIPPED_DICE, EquippedDiceId);
            PlayerPrefs.SetString(PREF_EQUIPPED_CUP, EquippedCupId);
            
            PlayerPrefs.Save();
        }
    }
}