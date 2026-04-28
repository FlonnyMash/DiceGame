using DiceGame.Core.Models;
using System;

namespace DiceGame.Services.Interfaces
{
    public interface ISettingsService
    {
        // Benachrichtigt z.B. den AudioManager, wenn Regler im UI verschoben werden
        event Action<AppSettings> OnSettingsChanged;

        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }
}