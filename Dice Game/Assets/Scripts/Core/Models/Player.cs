namespace DiceGame.Core.Models
{
    public class Player
    {
        public string Name { get; private set; }
        public ScoreCard ScoreCard { get; private set; }
        
        // NEU: Damit die Logik weiß, wer hier spielt
        public bool IsBot { get; private set; } 

        public Player(string name, bool isBot = false)
        {
            Name = name;
            IsBot = isBot;
            ScoreCard = new ScoreCard();
        }
    }
}