namespace DiceGame.Core.Models
{
    // Speichert dauerhafte Einstellungen des Spielers (wird auf dem Gerät gesichert)
    public class AppSettings
    {
        public float MusicVolume { get; set; } = 1.0f; // Standardwert 100%
        public float SfxVolume { get; set; } = 1.0f;   // Standardwert 100%
    }
}