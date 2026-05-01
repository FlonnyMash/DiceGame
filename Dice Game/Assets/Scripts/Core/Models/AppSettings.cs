using DiceGame.Services.Interfaces; // Für SupportedLanguage

namespace DiceGame.Core.Models
{
    public class AppSettings
    {
        public bool IsMusicOn { get; set; } = true;
        public bool IsSfxOn { get; set; } = true;
        
        // NEU: Die gespeicherte Sprache im Modell
        public SupportedLanguage Language { get; set; } = SupportedLanguage.English;
    }
}