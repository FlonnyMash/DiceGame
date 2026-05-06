using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using DiceGame.Configs;
using DiceGame.Core.Models;

namespace DiceGame.EditorTools
{
    public class ShopManagerWindow : EditorWindow
    {
        private Vector2 _scrollPos;
        private Dictionary<ShopItemType, bool> _categoryFoldouts = new Dictionary<ShopItemType, bool>();
        private string _folderToDeletePath = null;
        private string _newItemId = "new_dice_01";
        private ShopItemType _newItemType = ShopItemType.DiceSkin;

        [MenuItem("DiceGame/Tools/Shop Manager")]
        public static void ShowWindow()
        {
            GetWindow<ShopManagerWindow>("Shop Manager");
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_folderToDeletePath))
            {
                AssetDatabase.DeleteAsset(_folderToDeletePath);
                AssetDatabase.SaveAssets();
                _folderToDeletePath = null;
                GUIUtility.ExitGUI();
            }

            GUILayout.Label("Shop Items Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            GUILayout.Label("Create New Item", EditorStyles.boldLabel);
            _newItemId = EditorGUILayout.TextField("Item ID", _newItemId);
            _newItemType = (ShopItemType)EditorGUILayout.EnumPopup("Item Type", _newItemType);
            
            if (GUILayout.Button("Create Item & Folder", GUILayout.Height(30)))
            {
                CreateNewItem();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();
            GUILayout.Label("Existing Items", EditorStyles.boldLabel);
            
            ShopItemConfig[] configs = Resources.LoadAll<ShopItemConfig>("ShopItems");

            if (configs.Length == 0)
            {
                EditorGUILayout.HelpBox("Keine Shop Items im Ordner 'Assets/Resources/ShopItems' gefunden.", MessageType.Warning);
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            var groupedConfigs = configs.GroupBy(c => c.Type).OrderBy(g => g.Key);

            foreach (var group in groupedConfigs)
            {
                ShopItemType category = group.Key;

                if (!_categoryFoldouts.ContainsKey(category)) _categoryFoldouts[category] = true;

                EditorGUILayout.Space();
                
                GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldoutHeader)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 12
                };
                
                _categoryFoldouts[category] = EditorGUILayout.Foldout(
                    _categoryFoldouts[category], 
                    $"{category.ToString().ToUpper()} ({group.Count()} Items)", 
                    true, 
                    foldoutStyle
                );

                if (_categoryFoldouts[category])
                {
                    EditorGUI.indentLevel++; 

                    foreach (var config in group)
                    {
                        EditorGUILayout.BeginVertical("box");
                        EditorGUILayout.BeginHorizontal();
                        
                        if (config.ShopIcon != null)
                            GUILayout.Label(config.ShopIcon.texture, GUILayout.Width(50), GUILayout.Height(50));
                        else
                            GUILayout.Box("No Icon", GUILayout.Width(50), GUILayout.Height(50));

                        EditorGUILayout.BeginVertical();
                        
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.TextField("ID (Locked)", config.Id);
                        EditorGUI.EndDisabledGroup();
                        
                        config.Type = (ShopItemType)EditorGUILayout.EnumPopup("Type", config.Type);
                        
                        // NEU: Toggle für Default Item
                        config.IsDefaultItem = EditorGUILayout.Toggle("Is Default (Free)", config.IsDefaultItem);
                        
                        // Preis ausgrauen, wenn es ein Default-Item ist (da es eh gratis sein sollte)
                        EditorGUI.BeginDisabledGroup(config.IsDefaultItem);
                        config.Price = EditorGUILayout.IntField("Price", config.IsDefaultItem ? 0 : config.Price);
                        EditorGUI.EndDisabledGroup();
                        
                        EditorGUILayout.Space();
                        config.NameLocKey = EditorGUILayout.TextField("Name LocKey", config.NameLocKey);
                        config.DescLocKey = EditorGUILayout.TextField("Desc LocKey", config.DescLocKey);
                        
                        EditorGUILayout.Space();
                        config.ShopIcon = (Sprite)EditorGUILayout.ObjectField("Shop Icon UI", config.ShopIcon, typeof(Sprite), false);
                        
                        if (config.Type == ShopItemType.DiceSkin)
                        {
                            config.DiceSkin = (DiceSkinConfig)EditorGUILayout.ObjectField("Dice Skin Config", config.DiceSkin, typeof(DiceSkinConfig), false);
                            
                            if (config.DiceSkin != null)
                            {
                                EditorGUILayout.Space();
                                EditorGUILayout.BeginVertical("helpbox");
                                GUILayout.Label("Inline Dice Skin Editor", EditorStyles.miniBoldLabel);
                                
                                if (config.DiceSkin.Faces == null || config.DiceSkin.Faces.Length != 6)
                                {
                                    System.Array.Resize(ref config.DiceSkin.Faces, 6);
                                }

                                EditorGUI.BeginChangeCheck();
                                EditorGUILayout.BeginHorizontal();
                                for (int i = 0; i < 6; i++)
                                {
                                    EditorGUILayout.BeginVertical();
                                    GUILayout.Label($"Face {i + 1}", EditorStyles.miniLabel, GUILayout.Width(50));
                                    config.DiceSkin.Faces[i] = (Sprite)EditorGUILayout.ObjectField(
                                        config.DiceSkin.Faces[i], 
                                        typeof(Sprite), 
                                        false, 
                                        GUILayout.Width(50), 
                                        GUILayout.Height(50)
                                    );
                                    EditorGUILayout.EndVertical();
                                }
                                EditorGUILayout.EndHorizontal();
                                
                                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(config.DiceSkin);
                                EditorGUILayout.EndVertical();
                            }
                        }
                        
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.EndHorizontal();
                        
                        EditorGUILayout.Space();
                        EditorGUILayout.BeginHorizontal();
                        
                        if (GUILayout.Button("Ping in Project", GUILayout.Width(120)))
                        {
                            EditorGUIUtility.PingObject(config);
                            Selection.activeObject = config;
                        }

                        GUILayout.FlexibleSpace();

                        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); 
                        if (GUILayout.Button("Delete Item & Folder", GUILayout.Width(140)))
                        {
                            if (EditorUtility.DisplayDialog("Delete Shop Item", 
                                $"Möchtest du das Item '{config.Id}' und seinen kompletten Ordner wirklich löschen?", 
                                "Ja, Ordner löschen", "Abbrechen"))
                            {
                                string assetPath = AssetDatabase.GetAssetPath(config);
                                _folderToDeletePath = Path.GetDirectoryName(assetPath); 
                            }
                        }
                        GUI.backgroundColor = Color.white; 

                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.EndVertical();
                        EditorGUILayout.Space();

                        if (GUI.changed) EditorUtility.SetDirty(config);
                    }
                    EditorGUI.indentLevel--; 
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void CreateNewItem()
        {
            if (string.IsNullOrWhiteSpace(_newItemId)) return;

            string basePath = "Assets/Resources/ShopItems";
            string folderPath = $"{basePath}/{_newItemId}";

            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(basePath)) AssetDatabase.CreateFolder("Assets/Resources", "ShopItems");

            if (AssetDatabase.IsValidFolder(folderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Ein Ordner/Item mit der ID '{_newItemId}' existiert bereits!", "OK");
                return;
            }

            AssetDatabase.CreateFolder(basePath, _newItemId);

            ShopItemConfig shopConfig = CreateInstance<ShopItemConfig>();
            shopConfig.Id = _newItemId;
            shopConfig.Type = _newItemType;
            shopConfig.NameLocKey = $"shop_item_{_newItemId}";
            shopConfig.DescLocKey = $"shop_desc_{_newItemId}";

            if (_newItemType == ShopItemType.DiceSkin)
            {
                DiceSkinConfig skinConfig = CreateInstance<DiceSkinConfig>();
                skinConfig.Id = _newItemId;
                AssetDatabase.CreateAsset(skinConfig, $"{folderPath}/{_newItemId}_DiceSkin.asset");
                shopConfig.DiceSkin = skinConfig; 
            }

            AssetDatabase.CreateAsset(shopConfig, $"{folderPath}/{_newItemId}_ShopItem.asset");
            AssetDatabase.SaveAssets();
            
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = shopConfig;
        }
    }
}