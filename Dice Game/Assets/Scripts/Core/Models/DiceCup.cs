using System;
using System.Collections.Generic;

namespace DiceGame.Core.Models
{
    public class DiceCup
    {
        public List<Die> Dice { get; private set; }
        public int RollsLeft { get; private set; }
        public const int MaxRolls = 3;

        public event Action OnDiceRolled;

        private Random _rng;

        public DiceCup(int seed = 0)
        {
            // Wenn ein Seed übergeben wird, ist das Ergebnis deterministisch.
            // Perfekt für synchronisierten Online-Multiplayer.
            _rng = seed == 0 ? new Random() : new Random(seed);
            
            Dice = new List<Die>(5);
            for (int i = 0; i < 5; i++)
            {
                Dice.Add(new Die());
            }
            
            ResetTurn();
        }

        public void ResetTurn()
        {
            RollsLeft = MaxRolls;
            foreach (var die in Dice)
            {
                die.Reset();
            }
        }

        public bool Roll()
        {
            if (RollsLeft <= 0) return false;

            foreach (var die in Dice)
            {
                die.Roll(_rng);
            }
            
            RollsLeft--;
            OnDiceRolled?.Invoke();
            
            return true;
        }
    }
}