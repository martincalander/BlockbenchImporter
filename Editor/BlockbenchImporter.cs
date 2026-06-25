using System;
using System.Collections.Generic;
using System.IO;
using Essentials.BlockbenchImporter.Builders;
using Essentials.BlockbenchImporter.Model;
using Essentials.BlockbenchImporter.Parsing;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Essentials.BlockbenchImporter
{
    /// <summary>
    /// Unity ScriptedImporter for Blockbench .bbmodel files.
    /// Imports Blockbench models with support for:
    /// - Groups/hierarchy with correct pivots and transforms
    /// - Cube and mesh elements
    /// - Multiple textures with per-face assignment
    /// - Animations with Mecanim Animator support
    /// </summary>
    [ScriptedImporter(13, "bbmodel")]
    public class BlockbenchImporter : ScriptedImporter
    {
        [Header("Scale Settings")]
        [Tooltip("Global scale multiplier applied to the model")]
        public float scale = 1f;

        [Tooltip("How to interpret Blockbench units")]
        public UnitsMode unitsMode = UnitsMode.SixteenPixelsPerUnit;

        [Header("Texture Settings")]
        [Tooltip("Import textures embedded in the .bbmodel file")]
        public bool importTextures = true;

        [Tooltip("Use point filtering for pixel art style")]
        public bool pixelArt = true;

        [Tooltip("Force alpha cutout mode on materials to prevent see-through artifacts")]
        public bool forceAlphaClip = true;

        [Tooltip("Alpha cutoff threshold for cutout materials")]
        [Range(0f, 1f)]
        public float alphaCutoff = 0.5f;

        [Header("Animation Settings")]
        [Tooltip("Import animations from the .bbmodel file as AnimationClips")]
        public bool importAnimations = true;

        [Tooltip("Create an AnimatorController (experimental - may have issues with sub-asset serialization)")]
        public bool createAnimatorController = false;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                // Read and parse the .bbmodel JSON
                string json = File.ReadAllText(ctx.assetPath);
                BbModel model = BbModelParser.Parse(json);

                if (model == null)
                {
                    ctx.LogImportError("Failed to parse .bbmodel file");
                    return;
                }

                // Create settings from importer fields
                var settings = new BlockbenchImporterSettings
                {
                    scale = scale,
                    unitsMode = unitsMode,
                    importTextures = importTextures,
                    pixelArt = pixelArt,
                    forceAlphaClip = forceAlphaClip,
                    alphaCutoff = alphaCutoff,
                    importAnimations = importAnimations,
                    createAnimatorController = createAnimatorController
                };

                // Build textures and materials
                var materialBuilder = new MaterialBuilder(model, ctx.assetPath, settings);

                if (importTextures)
                {
                    materialBuilder.BuildTextures();
                }

                materialBuilder.BuildMaterials();

                // Build hierarchy
                var hierarchyBuilder = new HierarchyBuilder(model, settings, materialBuilder);
                GameObject root = hierarchyBuilder.BuildHierarchy();

                if (root == null)
                {
                    ctx.LogImportError("Failed to build model hierarchy");
                    return;
                }

                // IMPORTANT: Add root GameObject FIRST and set as main object immediately
                // This ensures the prefab is the main asset, not textures/materials
                string rootName = root.name;
                ctx.AddObjectToAsset(rootName, root);
                ctx.SetMainObject(root);

                // Track used identifiers to ensure uniqueness
                var usedIdentifiers = new HashSet<string>();
                usedIdentifiers.Add(rootName);

                // Add sub-assets: textures
                foreach (var texture in materialBuilder.GetAllTextures())
                {
                    string identifier = GetUniqueIdentifier($"tex_{texture.name}", usedIdentifiers);
                    ctx.AddObjectToAsset(identifier, texture);
                }

                // Add sub-assets: materials
                foreach (var material in materialBuilder.GetAllMaterials())
                {
                    string identifier = GetUniqueIdentifier($"mat_{material.name}", usedIdentifiers);
                    ctx.AddObjectToAsset(identifier, material);
                }

                // Add sub-assets: meshes
                foreach (var mesh in hierarchyBuilder.BuiltMeshes)
                {
                    string identifier = GetUniqueIdentifier($"mesh_{mesh.name}", usedIdentifiers);
                    ctx.AddObjectToAsset(identifier, mesh);
                }

                // Build animations if enabled
                if (importAnimations && model.animations != null && model.animations.Count > 0)
                {
                    var animationBuilder = new AnimationBuilder(
                        model,
                        hierarchyBuilder.UuidToPath,
                        hierarchyBuilder.UuidToTransform,
                        settings
                    );

                    var clips = animationBuilder.BuildAnimationClips();

                    // Add animation clips as sub-assets
                    foreach (var clip in clips)
                    {
                        // Mark as legacy clip for Animation component compatibility
                        clip.legacy = !createAnimatorController;
                        string identifier = GetUniqueIdentifier($"anim_{clip.name}", usedIdentifiers);
                        ctx.AddObjectToAsset(identifier, clip);
                    }

                    if (clips.Count > 0)
                    {
                        if (createAnimatorController)
                        {
                            // Experimental: Create animator controller
                            var controllerResult = animationBuilder.BuildAnimatorController(clips, ctx.assetPath);
                            if (controllerResult != null && controllerResult.controller != null)
                            {
                                // Add controller as sub-asset
                                string ctrlId = GetUniqueIdentifier($"ctrl_{controllerResult.controller.name}", usedIdentifiers);
                                ctx.AddObjectToAsset(ctrlId, controllerResult.controller);
                                
                                // Add internal objects (state machine, states) as hidden sub-assets
                                int subAssetIndex = 0;
                                foreach (var subAsset in controllerResult.subAssets)
                                {
                                    string internalId = GetUniqueIdentifier($"ctrl_internal_{subAssetIndex++}", usedIdentifiers);
                                    ctx.AddObjectToAsset(internalId, subAsset);
                                }

                                // Add Animator component to root
                                var animator = root.AddComponent<Animator>();
                                animator.runtimeAnimatorController = controllerResult.controller;
                            }
                        }
                        else
                        {
                            // Use legacy Animation component (more reliable for sub-assets)
                            var animation = root.AddComponent<Animation>();
                            foreach (var clip in clips)
                            {
                                animation.AddClip(clip, clip.name);
                            }
                            if (clips.Count > 0)
                            {
                                animation.clip = clips[0];
                            }
                        }
                    }
                }

                // Strip BlockbenchOriginMarker components (editor-only)
                var markers = root.GetComponentsInChildren<BlockbenchOriginMarker>(true);
                foreach (var marker in markers)
                {
                    DestroyImmediate(marker);
                }
            }
            catch (Exception ex)
            {
                ctx.LogImportError($"Failed to import .bbmodel: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Get a unique identifier for a sub-asset, appending a number if necessary.
        /// </summary>
        private static string GetUniqueIdentifier(string baseName, HashSet<string> usedIdentifiers)
        {
            if (!usedIdentifiers.Contains(baseName))
            {
                usedIdentifiers.Add(baseName);
                return baseName;
            }

            // Find a unique suffix
            int counter = 2;
            string uniqueName;
            do
            {
                uniqueName = $"{baseName}_{counter}";
                counter++;
            } while (usedIdentifiers.Contains(uniqueName));

            usedIdentifiers.Add(uniqueName);
            return uniqueName;
        }
    }

    /// <summary>
    /// Custom editor for the BlockbenchImporter with preview and validation.
    /// </summary>
    [CustomEditor(typeof(BlockbenchImporter))]
    public class BlockbenchImporterEditor : ScriptedImporterEditor
    {
        private SerializedProperty scaleProp;
        private SerializedProperty unitsModeProp;
        private SerializedProperty importTexturesProp;
        private SerializedProperty pixelArtProp;
        private SerializedProperty forceAlphaClipProp;
        private SerializedProperty alphaCutoffProp;
        private SerializedProperty importAnimationsProp;
        private SerializedProperty createAnimatorControllerProp;

        public override void OnEnable()
        {
            base.OnEnable();

            scaleProp = serializedObject.FindProperty("scale");
            unitsModeProp = serializedObject.FindProperty("unitsMode");
            importTexturesProp = serializedObject.FindProperty("importTextures");
            pixelArtProp = serializedObject.FindProperty("pixelArt");
            forceAlphaClipProp = serializedObject.FindProperty("forceAlphaClip");
            alphaCutoffProp = serializedObject.FindProperty("alphaCutoff");
            importAnimationsProp = serializedObject.FindProperty("importAnimations");
            createAnimatorControllerProp = serializedObject.FindProperty("createAnimatorController");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Blockbench Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Scale settings
            EditorGUILayout.LabelField("Scale Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(scaleProp, new GUIContent("Scale", "Global scale multiplier"));
            EditorGUILayout.PropertyField(unitsModeProp, new GUIContent("Units Mode", "How to interpret Blockbench units"));

            EditorGUILayout.Space();

            // Texture settings
            EditorGUILayout.LabelField("Texture Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(importTexturesProp, new GUIContent("Import Textures", "Import embedded textures"));

            using (new EditorGUI.DisabledScope(!importTexturesProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(pixelArtProp, new GUIContent("Pixel Art Mode", "Use point filtering"));
                EditorGUILayout.PropertyField(forceAlphaClipProp, new GUIContent("Force Alpha Clip", "Prevent see-through artifacts"));
                EditorGUILayout.PropertyField(alphaCutoffProp, new GUIContent("Alpha Cutoff", "Cutoff threshold"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Animation settings
            EditorGUILayout.LabelField("Animation Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(importAnimationsProp, new GUIContent("Import Animations", "Import Blockbench animations"));

            using (new EditorGUI.DisabledScope(!importAnimationsProp.boolValue))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(createAnimatorControllerProp, new GUIContent("Create Animator Controller", "Generate Mecanim controller"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            // Info box
            EditorGUILayout.HelpBox(
                "Blockbench Importer imports .bbmodel files with support for:\n" +
                "• Groups/hierarchy with correct pivots\n" +
                "• Cube and mesh elements\n" +
                "• Multiple textures per model\n" +
                "• Animations with Mecanim support",
                MessageType.Info);

            serializedObject.ApplyModifiedProperties();

            ApplyRevertGUI();
        }
    }
}
