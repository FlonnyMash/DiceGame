using System;
using System.Collections.Generic;

namespace DiceGame.Core.Models
{
    public class Inventory
    {
        private readonly HashSet<string> _unlockedItemIds;
        public event Action<string> OnItemUnlocked;

        public Inventory(IEnumerable<string> initialUnlocks = null)
        {
            _unlockedItemIds = new HashSet<string>();
            if (initialUnlocks != null)
            {
                foreach (var id in initialUnlocks)
                {
                    _unlockedItemIds.Add(id);
                }
            }
        }

        public bool HasItem(string itemId) => _unlockedItemIds.Contains(itemId);

        public bool UnlockItem(string itemId)
        {
            if (_unlockedItemIds.Contains(itemId)) return false; // Bereits in Besitz
            
            _unlockedItemIds.Add(itemId);
            OnItemUnlocked?.Invoke(itemId);
            return true;
        }

        public IEnumerable<string> GetAllUnlockedItems() => _unlockedItemIds;
    }
}