namespace DiceGame.Core.Models
{
    public class Player
    {
        public string Name { get; private set; }
        public ScoreCard ScoreCard { get; private set; }

        public Player(string name)
        {
            Name = name;
            ScoreCard = new ScoreCard();
        }
    }
}