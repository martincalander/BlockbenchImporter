using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Custom editor for the Blockbench importer settings.
    /// </summary>
    [CustomEditor(typeof(BlockbenchImporter))]
    public class BlockbenchImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty scaleFactorProp;
        private SerializedProperty generateMaterialProp;
        private SerializedProperty defaultColorProp;
        private SerializedProperty importAnimationsProp;
        
        private bool showModelInfo = true;
        
        public override void OnEnable()
        {
            base.OnEnable();
            
            scaleFactorProp = serializedObject.FindProperty("scaleFactor");
            generateMaterialProp = serializedObject.FindProperty("generateMaterial");
            defaultColorProp = serializedObject.FindProperty("defaultColor");
            importAnimationsProp = serializedObject.FindProperty("importAnimations");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.LabelField("Blockbench Import Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Model Settings
            EditorGUILayout.LabelField("Model", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(scaleFactorProp, new GUIContent("Scale Factor", "Scale factor applied to the imported model"));
            
            EditorGUILayout.Space();
            
            // Material Settings
            EditorGUILayout.LabelField("Material", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(generateMaterialProp, new GUIContent("Generate Material", "Create a default material for the model"));
            
            if (generateMaterialProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(defaultColorProp, new GUIContent("Default Color", "Color for the generated material when no texture is embedded"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // Animation Settings
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(importAnimationsProp, new GUIContent("Import Animations", "Import animations from the Blockbench model"));
            
            EditorGUILayout.Space();
            
            // Model Info Section
            showModelInfo = EditorGUILayout.Foldout(showModelInfo, "Model Information", true);
            if (showModelInfo)
            {
                var importer = target as BlockbenchImporter;
                if (importer != null)
                {
                    try
                    {
                        string json = System.IO.File.ReadAllText(importer.assetPath);
                        var model = BlockbenchJsonParser.Parse(json);
                        
                        if (model != null)
                        {
                            EditorGUI.indentLevel++;
                            
                            EditorGUILayout.LabelField("Name", model.name ?? "(unnamed)");
                            
                            if (model.meta != null)
                            {
                                EditorGUILayout.LabelField("Format Version", model.meta.format_version ?? "unknown");
                                EditorGUILayout.LabelField("Model Format", model.meta.model_format ?? "unknown");
                            }
                            
                            if (model.resolution != null)
                            {
                                EditorGUILayout.LabelField("Resolution", $"{model.resolution.width} x {model.resolution.height}");
                            }
                            
                            int elementCount = model.elements?.Count ?? 0;
                            EditorGUILayout.LabelField("Elements", elementCount.ToString());
                            
                            int textureCount = model.textures?.Count ?? 0;
                            EditorGUILayout.LabelField("Textures", textureCount.ToString());
                            
                            int animationCount = model.animations?.Count ?? 0;
                            EditorGUILayout.LabelField("Animations", animationCount.ToString());
                            
                            EditorGUI.indentLevel--;
                        }
                    }
                    catch
                    {
                        EditorGUILayout.HelpBox("Could not read model information.", MessageType.Warning);
                    }
                }
            }
            
            serializedObject.ApplyModifiedProperties();
            
            ApplyRevertGUI();
        }
    }
}
