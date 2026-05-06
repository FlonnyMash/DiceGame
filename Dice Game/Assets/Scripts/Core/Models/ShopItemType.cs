namespace DiceGame.Core.Models
{
    /// <summary>
    /// Definiert die Art des Items. Hält die Tür offen für spätere Gameplay-Erweiterungen.
    /// </summary>
    public enum ShopItemType
    {
        // Kosmetisch (Jetzt)
        DiceSkin,
        CupSkin,
        BoardTheme,
        ScoreCardTheme,

        // Gameplay / Meta (Zukunft)
        ArcadeBooster,
        CoinMultiplier
    }
}