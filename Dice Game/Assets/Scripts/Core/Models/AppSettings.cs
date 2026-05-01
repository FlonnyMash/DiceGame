using DiceGame.Services.Interfaces; // Für SupportedLanguage

namespace DiceGame.Core.Models
{
    public class AppSettings
    {
        // NEU: Die gespeicherte Master-Lautstärke
        public bool IsMasterOn { get; set; } = true; 
        
        public bool IsMusicOn { get; set; } = true;
        public bool IsSfxOn { get; set; } = true;
        
        // Die gespeicherte Sprache im Modell
        public SupportedLanguage Language { get; set; } = SupportedLanguage.English;
    }
}