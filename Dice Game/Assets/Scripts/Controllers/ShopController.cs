using UnityEngine;
using DiceGame.UI.Views;
using DiceGame.Services;
using DiceGame.Core.Models;
using DiceGame.Services.Interfaces;

namespace DiceGame.Controllers
{
    public class ShopController : MonoBehaviour
    {
        [SerializeField] private ShopView _shopView;
        private IEconomyService _economyService;

        private void Start()
        {
            _economyService = PlayerPrefsEconomyService.Instance;

            // Name des Events an die ShopView angepasst
            _shopView.OnItemActionClicked += HandleItemActionClicked; 
            _shopView.OnCloseClicked += _shopView.Hide;

            // Auf Änderungen hören
            _economyService.PlayerWallet.OnCoinsChanged += HandleEconomyChanged;
            _economyService.OnLoadoutChanged += () => HandleEconomyChanged(_economyService.PlayerWallet.Coins);
            
            RefreshFullShop();
            _shopView.Hide();
        }

        public void OpenShop()
        {
            RefreshFullShop();
            _shopView.Show();
        }

        private void RefreshFullShop()
        {
            _shopView.UpdateCoinsDisplay(_economyService.PlayerWallet.Coins);
            _shopView.Populate(
                _economyService.GetAvailableShopItems(), 
                _economyService.PlayerInventory, 
                _economyService.PlayerWallet.Coins,
                _economyService.EquippedDiceId,
                _economyService.EquippedCupId
            );
        }

        private void HandleEconomyChanged(int currentCoins)
        {
            _shopView.UpdateCoinsDisplay(currentCoins);
            _shopView.UpdateAllItemStates(
                _economyService.PlayerInventory, 
                currentCoins,
                _economyService.EquippedDiceId,
                _economyService.EquippedCupId
            );
        }

        private void HandleItemActionClicked(ShopItem item)
        {
            if (_economyService.PlayerInventory.HasItem(item.Id))
            {
                // Hat er schon -> Ausrüsten
                _economyService.EquipItem(item);
            }
            else
            {
                // Hat er nicht -> Kaufen
                bool success = _economyService.PurchaseItem(item);
                if (success)
                {
                    _economyService.EquipItem(item); // Direkt nach Kauf automatisch ausrüsten
                    
                    // TODO: AudioManager.Instance.PlaySFX(_buySound);
                }
            }
        }

        private void OnDestroy()
        {
            if (_economyService != null)
            {
                if (_economyService.PlayerWallet != null) _economyService.PlayerWallet.OnCoinsChanged -= HandleEconomyChanged;
                _economyService.OnLoadoutChanged -= () => HandleEconomyChanged(_economyService.PlayerWallet.Coins);
            }
        }
    }
}