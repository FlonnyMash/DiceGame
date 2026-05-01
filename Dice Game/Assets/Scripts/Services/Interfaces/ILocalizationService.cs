using System;

namespace DiceGame.Services.Interfaces
{
    // Dieses Enum muss hier stehen, damit alle Systeme wissen, welche Sprachen es gibt
    public enum SupportedLanguage 
    { 
        English, 
        German, 
        Spanish,
        French,
        Italian,
        Portuguese
    }

    public interface ILocalizationService
    {
        event Action OnLanguageChanged;
        
        SupportedLanguage CurrentLanguage { get; }
        void SetLanguage(SupportedLanguage language);
        
        string GetText(string key, params object[] args);
    }
}