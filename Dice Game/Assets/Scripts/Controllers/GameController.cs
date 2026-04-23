using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DiceGame.Core.Models;
using DiceGame.UI.Views;

namespace DiceGame.Controllers
{
    public class GameController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private List<DieView> _dieViews;
        [SerializeField] private Button _rollButton;

        private DiceCup _diceCup;

        private void Start()
        {
            _diceCup = new DiceCup();
            
            // Verbinde die UI-Würfel mit den Core-Würfeln
            for (int i = 0; i < _dieViews.Count; i++)
            {
                int index = i; // Wichtig für die korrekte Event-Zuweisung in der Schleife
                
                // 1. Initialisiere die View mit ihrem Index
                _dieViews[i].Initialize(index);
                
                // 2. Höre auf Klicks aus der UI
                _dieViews[i].OnDieClicked += HandleDieClicked;
                
                // 3. Höre auf Wertänderungen aus der Core-Logik und update die View
                _diceCup.Dice[i].OnStateChanged += (die) => _dieViews[index].UpdateView(die.Value, die.IsHeld);
            }

            // Roll-Button mit der Becher-Logik verbinden
            _rollButton.onClick.AddListener(OnRollButtonClicked);
            
            // Einmaliges Start-Update für die Optik
            _diceCup.ResetTurn(); 
        }

        private void OnRollButtonClicked()
        {
            bool success = _diceCup.Roll();
            if (!success)
            {
                Debug.Log("Keine Würfe mehr übrig! Wähle eine Kategorie.");
                _rollButton.interactable = false; // Button deaktivieren
            }
        }

        private void HandleDieClicked(int dieIndex)
        {
            // Reiche den Klick an den Core-Würfel weiter
            _diceCup.Dice[dieIndex].ToggleHold();
        }

        private void OnDestroy()
        {
            if (_rollButton != null) _rollButton.onClick.RemoveAllListeners();
            // DieView-Events werden von den Views selbst im OnDestroy aufgeräumt
        }
    }
}