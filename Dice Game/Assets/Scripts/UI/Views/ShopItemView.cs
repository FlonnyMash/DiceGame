using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System;
using UnityEngine.Serialization;
using DiceGame.Core.Models;
using DiceGame.Services;
using DiceGame.Configs; // NEU: Damit wir die Unity-Configs laden können

namespace DiceGame.UI.Views
{
    public class ShopItemView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descText;
        [SerializeField] private TextMeshProUGUI _priceText;
        
        [FormerlySerializedAs("_buyButton")]
        [SerializeField] private Button _actionButton;
        [SerializeField] private Image _coinIcon;
        
        // NEU: Referenz auf das Bild des Items (Würfel, Becher, etc.)
        [SerializeField] private Image _itemIcon;
        
        [Header("Colors")]
        [SerializeField] private Color _affordableColor = Color.white;
        [SerializeField] private Color _unaffordableColor = Color.red;

        private ShopItem _currentItem;
        private TextAlignmentOptions _originalPriceAlignment; 
        
        public event Action<ShopItem> OnItemActionClicked;

        public void Initialize(ShopItem item, bool isOwned, bool isEquipped, int currentCoins)
        {
            _currentItem = item;
            
            if (_nameText != null) _nameText.text = LocalizationService.Instance.GetText(item.NameLocKey);
            if (_descText != null) _descText.text = LocalizationService.Instance.GetText(item.DescLocKey);
            
            if (_priceText != null) _originalPriceAlignment = _priceText.alignment;
            
            // NEU: Das Shop-Icon dynamisch aus den Resources laden
            if (_itemIcon != null)
            {
                // Wir durchsuchen alle Configs im Ordner "Resources/ShopItems" nach der passenden ID
                ShopItemConfig[] configs = Resources.LoadAll<ShopItemConfig>("ShopItems");
                foreach (var config in configs)
                {
                    if (config.Id == item.Id && config.ShopIcon != null)
                    {
                        _itemIcon.sprite = config.ShopIcon;
                        break;
                    }
                }
            }
            
            if (_actionButton != null)
            {
                _actionButton.onClick.RemoveAllListeners();
                _actionButton.onClick.AddListener(() => OnItemActionClicked?.Invoke(_currentItem));
            }
            else
            {
                Debug.LogError($"[ShopItemView] Der '_actionButton' fehlt auf dem Prefab '{gameObject.name}'! Bitte im Inspector zuweisen.", this);
            }

            UpdateState(isOwned, isEquipped, currentCoins);
        }

        public void UpdateState(bool isOwned, bool isEquipped, int currentCoins)
        {
            if (_priceText == null || _actionButton == null) return;

            if (isEquipped)
            {
                _priceText.text = LocalizationService.Instance.GetText("shop_item_equipped");
                _priceText.color = _affordableColor; 
                _priceText.alignment = TextAlignmentOptions.Center; 
                _actionButton.interactable = false; 
                if (_coinIcon != null) _coinIcon.gameObject.SetActive(false); 
            }
            else if (isOwned)
            {
                _priceText.text = LocalizationService.Instance.GetText("shop_item_equip");
                _priceText.color = _affordableColor; 
                _priceText.alignment = TextAlignmentOptions.Center; 
                _actionButton.interactable = true; 
                if (_coinIcon != null) _coinIcon.gameObject.SetActive(false); 
            }
            else
            {
                if (_currentItem.Price == 0)
                {
                    _priceText.text = LocalizationService.Instance.GetText("shop_item_free");
                    _priceText.color = _affordableColor;
                    _priceText.alignment = TextAlignmentOptions.Center; 
                    _actionButton.interactable = true;
                    if (_coinIcon != null) _coinIcon.gameObject.SetActive(false); 
                }
                else
                {
                    _priceText.text = _currentItem.Price.ToString();
                    
                    bool canAfford = currentCoins >= _currentItem.Price;
                    _priceText.color = canAfford ? _affordableColor : _unaffordableColor;
                    
                    _priceText.alignment = _originalPriceAlignment; 
                    
                    _actionButton.interactable = canAfford; 
                    if (_coinIcon != null) _coinIcon.gameObject.SetActive(true); 
                }
            }
        }
    }
}