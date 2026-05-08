using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DiceGame.EditorTools
{
    public class LocalizationCSVImporter : EditorWindow
    {
        private TextAsset _csvFile;
        private string _delimiter = ";"; // Standard für deutsches Excel, auf "," ändern falls nötig

        // Erstellt den Menüpunkt oben in der Unity-Leiste
        [MenuItem("DiceGame/Tools/Localization/Import CSV to JSON")]
        public static void ShowWindow()
        {
            GetWindow<LocalizationCSVImporter>("CSV Importer");
        }

        private void OnGUI()
        {
            GUILayout.Label("CSV zu JSON Converter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV Datei", _csvFile, typeof(TextAsset), false);
            _delimiter = EditorGUILayout.TextField("Trennzeichen (Delimiter)", _delimiter);

            EditorGUILayout.Space();

            if (GUILayout.Button("JSON Dateien generieren"))
            {
                if (_csvFile != null)
                {
                    ProcessCSV(_csvFile.text);
                }
                else
                {
                    EditorUtility.DisplayDialog("Fehler", "Bitte ziehe zuerst eine CSV-Datei in das Feld.", "OK");
                }
            }
        }

        private void ProcessCSV(string csvText)
        {
            // Zeilen aufteilen (unterstützt Windows \r\n und Mac \n)
            string[] lines = csvText.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            if (lines.Length < 2)
            {
                EditorUtility.DisplayDialog("Fehler", "Die CSV-Datei muss mindestens eine Kopfzeile und eine Datenzeile enthalten.", "OK");
                return;
            }

            // Kopfzeile auslesen (z.B. "Key;en;de")
            string[] headers = lines[0].Split(_delimiter.ToCharArray());
            
            // Ein Dictionary für jede Sprache anlegen (startet bei Index 1, da Index 0 der 'Key' ist)
            Dictionary<string, Dictionary<string, string>> languageDicts = new Dictionary<string, Dictionary<string, string>>();
            for (int i = 1; i < headers.Length; i++)
            {
                string langCode = headers[i].Trim();
                if (!string.IsNullOrEmpty(langCode))
                {
                    languageDicts[langCode] = new Dictionary<string, string>();
                }
            }

            // Alle restlichen Zeilen durchgehen
            for (int i = 1; i < lines.Length; i++)
            {
                string[] row = lines[i].Split(_delimiter.ToCharArray());
                if (row.Length == 0 || string.IsNullOrEmpty(row[0])) continue;

                string key = row[0].Trim();

                // Für jede Sprache den entsprechenden Wert aus der Spalte holen
                for (int j = 1; j < headers.Length; j++)
                {
                    if (j < row.Length)
                    {
                        string langCode = headers[j].Trim();
                        if (languageDicts.ContainsKey(langCode))
                        {
                            languageDicts[langCode][key] = row[j].Trim();
                        }
                    }
                }
            }

            // Zielordner: Resources/Localization, damit die JSONs auf allen Plattformen
            // (insbesondere Android, wo StreamingAssets im APK liegt und nicht per File-API
            // gelesen werden kann) synchron via Resources.Load geladen werden können.
            string folderPath = Path.Combine(Application.dataPath, "Resources", "Localization");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // JSON-Dateien für alle gefundenen Sprachen schreiben
            foreach (var kvp in languageDicts)
            {
                string langCode = kvp.Key;
                var dict = kvp.Value;

                // Dictionary in schönes JSON umwandeln
                string json = JsonConvert.SerializeObject(dict, Formatting.Indented);
                string filePath = Path.Combine(folderPath, $"{langCode}.json");
                
                File.WriteAllText(filePath, json);
                Debug.Log($"[Localization] {langCode}.json erfolgreich mit {dict.Count} Einträgen generiert!");
            }

            // Unity anweisen, die neuen Dateien sofort im Project-Fenster anzuzeigen
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Erfolg", "JSON Dateien wurden erfolgreich in Assets/Resources/Localization generiert!", "OK");
        }
    }
}