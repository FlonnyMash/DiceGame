namespace DiceGame.Core.Rules
{
    public enum ScoreCategory
    {
        // Oberer Block
        Ones,
        Twos,
        Threes,
        Fours,
        Fives,
        Sixes,

        // Unterer Block
        ThreeOfAKind,
        FourOfAKind,
        FullHouse,
        SmallStraight,
        LargeStraight,
        Yahtzee,
        Chance
    }
}