namespace DiceGame.Core.Models
{
    public class Player
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public ScoreCard ScoreCard { get; private set; }

        // True if this player is controlled by the local AI.
        public bool IsBot { get; private set; }

        // True if this player is owned by a remote peer (driven via NetworkPlayerInput).
        public bool IsRemote { get; private set; }

        public Player(string name, bool isBot = false)
            : this(0, name, isBot, false) { }

        public Player(int id, string name, bool isBot = false, bool isRemote = false)
        {
            Id = id;
            Name = name;
            IsBot = isBot;
            IsRemote = isRemote;
            ScoreCard = new ScoreCard();
        }
    }
}
