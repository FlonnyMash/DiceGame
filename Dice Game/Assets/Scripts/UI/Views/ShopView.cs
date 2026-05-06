using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using DiceGame.Core.Models;

namespace DiceGame.UI.Views
{
    public class ShopView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private ShopItemView _itemPrefab;
        [SerializeField] private Transform _itemsContainer;
        [SerializeField] private TextMeshProUGUI _coinsText;
        [SerializeField] private Button _closeButton;

        public event System.Action OnCloseClicked;
        // Event umbenannt, da es jetzt für "Buy" und "Equip" zuständig ist
        public event System.Action<ShopItem> OnItemActionClicked; 

        private Dictionary<string, ShopItemView> _itemViews = new Dictionary<string, ShopItemView>();

        private void Awake()
        {
            if (_closeButton != null) 
                _closeButton.onClick.AddListener(() => OnCloseClicked?.Invoke());
        }

        public void UpdateCoinsDisplay(int amount)
        {
            if (_coinsText != null) 
                _coinsText.text = amount.ToString();
        }

        // Signatur aktualisiert: Nimmt jetzt currentCoins und die Equipped-IDs entgegen
        public void Populate(IEnumerable<ShopItem> items, Inventory inventory, int currentCoins, string equippedDiceId, string equippedCupId)
        {
            foreach (Transform child in _itemsContainer) Destroy(child.gameObject);
            _itemViews.Clear();

            foreach (var item in items)
            {
                ShopItemView view = Instantiate(_itemPrefab, _itemsContainer);
                
                bool isOwned = inventory.HasItem(item.Id);
                bool isEquipped = (item.Id == equippedDiceId || item.Id == equippedCupId);
                
                // Nutzt nun die 4 Parameter der neuen Initialize-Methode
                view.Initialize(item, isOwned, isEquipped, currentCoins);
                view.OnItemActionClicked += (clickedItem) => OnItemActionClicked?.Invoke(clickedItem);
                
                _itemViews.Add(item.Id, view);
            }
        }

        // Signatur aktualisiert: Nimmt jetzt currentCoins und die Equipped-IDs entgegen
        public void UpdateAllItemStates(Inventory inventory, int currentCoins, string equippedDiceId, string equippedCupId)
        {
            foreach (var kvp in _itemViews)
            {
                bool isOwned = inventory.HasItem(kvp.Key);
                bool isEquipped = (kvp.Key == equippedDiceId || kvp.Key == equippedCupId);
                
                // Nutzt nun die 3 Parameter der neuen UpdateState-Methode
                kvp.Value.UpdateState(isOwned, isEquipped, currentCoins);
            }
        }

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}