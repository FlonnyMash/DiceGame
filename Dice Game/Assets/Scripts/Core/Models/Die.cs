using System;

namespace DiceGame.Core.Models
{
    public class Die
    {
        public int Value { get; private set; }
        public bool IsHeld { get; private set; }

        // C#-Events benachrichtigen später die UI, ohne dass der Core die UI kennen muss.
        public event Action<Die> OnStateChanged;

        public Die()
        {
            Value = 1;
            IsHeld = false;
        }

        // Wir übergeben Random von außen. Das ist später für Multiplayer/SharePlay
        // extrem wichtig, damit alle Spieler (mit dem gleichen Seed) dieselben Zahlen würfeln!
        public void Roll(Random rng)
        {
            if (IsHeld) return;
            
            Value = rng.Next(1, 7); // Generiert Zahlen von 1 bis 6
            OnStateChanged?.Invoke(this);
        }

        public void ToggleHold()
        {
            IsHeld = !IsHeld;
            OnStateChanged?.Invoke(this);
        }

        public void Reset()
        {
            IsHeld = false;
            OnStateChanged?.Invoke(this);
        }
    }
}