using System;

namespace DiceGame.Core.Models
{
    public class Wallet
    {
        public int Coins { get; private set; }
        public event Action<int> OnCoinsChanged;

        public Wallet(int startingCoins = 0)
        {
            Coins = startingCoins;
        }

        public void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
            OnCoinsChanged?.Invoke(Coins);
        }

        public bool SpendCoins(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            
            Coins -= amount;
            OnCoinsChanged?.Invoke(Coins);
            return true;
        }
    }
}