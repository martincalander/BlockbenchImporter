using System;
using System.Collections.Generic;
using System.IO;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Parsing;
using MartinCalander.BlockbenchImporter.Utils;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Serialization;

namespace MartinCalander.BlockbenchImporter
{
    [ScriptedImporter(20, "bbmodel")]
    public sealed class BlockbenchImporter : ScriptedImporter
    {
        [FormerlySerializedAs("unitsPerPixel")]
        [SerializeField, Min(0.000001f)] private float importScale = CoordinateConverter.DefaultImportScale;
        [SerializeField] private bool importTextures = true;
        [SerializeField] private bool importAnimations = true;
        [SerializeField] private bool createAnimatorController = true;
        [SerializeField] private BlockbenchMaterialMode materialMode = BlockbenchMaterialMode.Unlit;
        [SerializeField] private FilterMode textureFilter = FilterMode.Point;
        [SerializeField] private TextureWrapMode textureWrap = TextureWrapMode.Clamp;
        [SerializeField] private BlockbenchAlphaMode alphaMode = BlockbenchAlphaMode.Auto;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f;
        [SerializeField] private bool includeHiddenElements;
        [SerializeField] private bool includeNonExportedElements;
        [SerializeField] private bool importBoundingBoxesAsColliders = true;
        [SerializeField] private bool generateBackfacesForDoubleSidedTextures;

        /// <summary>
        /// Arguments supplied after a Blockbench model and all of its generated sub-assets have been imported.
        /// </summary>
        public readonly struct ImportEventArgs
        {
            /// <summary>The importer that fired the callback.</summary>
            public readonly BlockbenchImporter importer;

            /// <summary>The active import context. Use it to inspect or add generated sub-assets.</summary>
            public readonly AssetImportContext context;

            /// <summary>The generated root GameObject registered as the main asset.</summary>
            public readonly GameObject rootObject;

            internal ImportEventArgs(BlockbenchImporter importer, AssetImportContext context, GameObject rootObject)
            {
                this.importer = importer;
                this.context = context;
                this.rootObject = rootObject;
            }
        }

        /// <summary>Delegate used by Blockbench import callbacks.</summary>
        /// <param name="args">The importer, active context, and generated root object.</param>
        public delegate void BlockbenchImportEventHandler(ImportEventArgs args);

        /// <summary>
        /// Callback fired at the end of a successful Blockbench import, after the main object and generated
        /// textures, materials, meshes, animation clips, and Animator Controller have been registered.
        /// Subscribe from <c>AssetPostprocessor.OnPreprocessAsset</c> for the current importer instance.
        /// </summary>
        public BlockbenchImportEventHandler OnPostBlockbenchImport { get; set; }

        /// <summary>Unity units generated for one Blockbench model unit.</summary>
        public float ImportScale
        {
            get => importScale;
            set
            {
                importScale = Mathf.Max(0.000001f, value);
                EditorUtility.SetDirty(this);
            }
        }

        public override void OnImportAsset(AssetImportContext context)
        {
            try
            {
                BbModel model = BbModelParser.Parse(File.ReadAllText(context.assetPath));
                var settings = new BlockbenchImporterSettings
                {
                    importScale = Mathf.Max(0.000001f, importScale),
                    importTextures = importTextures,
                    importAnimations = importAnimations,
                    createAnimatorController = createAnimatorController,
                    materialMode = materialMode,
                    textureFilter = textureFilter,
                    textureWrap = textureWrap,
                    alphaMode = alphaMode,
                    alphaCutoff = alphaCutoff,
                    includeHiddenElements = includeHiddenElements,
                    includeNonExportedElements = includeNonExportedElements,
                    importBoundingBoxesAsColliders = importBoundingBoxesAsColliders,
                    generateBackfacesForDoubleSidedTextures = generateBackfacesForDoubleSidedTextures
                };

                RegisterTextureDependencies(context, model);

                var materials = new MaterialBuilder(model, context.assetPath, settings);
                materials.BuildTextures();
                materials.BuildMaterials();

                var hierarchy = new HierarchyBuilder(model, settings, materials);
                GameObject root = hierarchy.BuildHierarchy();
                foreach (string warning in model.warnings)
                    context.LogImportWarning(warning);
                foreach (string warning in hierarchy.Warnings)
                    context.LogImportWarning(warning);
                if (root == null)
                    throw new InvalidDataException("The Blockbench outliner did not produce a root object.");

                var identifiers = new HashSet<string>(StringComparer.Ordinal);
                Add(context, identifiers, "prefab", root);
                context.SetMainObject(root);

                foreach (Texture2D texture in materials.GetAllTextures())
                    Add(context, identifiers, "texture_" + texture.name, texture);
                foreach (Material material in materials.GetAllMaterials())
                    Add(context, identifiers, "material_" + material.name, material);
                foreach (Mesh mesh in hierarchy.BuiltMeshes)
                    Add(context, identifiers, "mesh_" + mesh.name, mesh);

                if (settings.importAnimations && model.animations.Count != 0)
                {
                    var animationBuilder = new AnimationBuilder(model, hierarchy.UuidToPath,
                        hierarchy.UuidToTransform, settings);
                    List<AnimationClip> clips = animationBuilder.BuildAnimationClips();
                    foreach (AnimationClip clip in clips)
                        Add(context, identifiers, "animation_" + clip.name, clip);

                    if (clips.Count != 0 && settings.createAnimatorController)
                    {
                        AnimatorControllerResult controller = animationBuilder.BuildAnimatorController(clips, context.assetPath);
                        if (controller != null)
                        {
                            Add(context, identifiers, "animator_controller", controller.controller);
                            foreach (UnityEngine.Object child in controller.subAssets)
                                Add(context, identifiers, "animator_" + child.name, child);
                            root.AddComponent<Animator>().runtimeAnimatorController = controller.controller;
                        }
                    }
                }

                OnPostBlockbenchImport?.Invoke(new ImportEventArgs(this, context, root));
            }
            catch (Exception exception)
            {
                context.LogImportError("Blockbench import failed: " + exception.Message + "\n" + exception.StackTrace);
            }
        }

        private static void Add(AssetImportContext context, HashSet<string> identifiers, string proposed,
            UnityEngine.Object value)
        {
            string clean = Sanitize(proposed);
            string unique = clean;
            for (int suffix = 2; !identifiers.Add(unique); suffix++)
                unique = clean + "_" + suffix;
            context.AddObjectToAsset(unique, value);
        }

        private static void RegisterTextureDependencies(AssetImportContext context, BbModel model)
        {
            string modelDirectory = Path.GetDirectoryName(context.assetPath) ?? string.Empty;
            string projectRoot = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/');
            foreach (BbTexture texture in model.textures)
            {
                RegisterDependency(context, modelDirectory, projectRoot, texture.relative_path);
                RegisterDependency(context, modelDirectory, projectRoot, texture.path);
            }
        }

        private static void RegisterDependency(AssetImportContext context, string modelDirectory,
            string projectRoot, string source)
        {
            if (string.IsNullOrEmpty(source)) return;
            string candidate = Path.IsPathRooted(source)
                ? Path.GetFullPath(source)
                : Path.GetFullPath(Path.Combine(modelDirectory, source));
            candidate = candidate.Replace('\\', '/');
            if (!candidate.StartsWith(projectRoot + "/", StringComparison.Ordinal)) return;
            string assetPath = candidate.Substring(projectRoot.Length + 1);
            if ((assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                 assetPath.StartsWith("Packages/", StringComparison.Ordinal)) && File.Exists(candidate))
                context.DependsOnSourceAsset(assetPath);
        }

        internal static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unnamed";
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim().Replace('/', '_');
        }
    }

    [CustomEditor(typeof(BlockbenchImporter))]
    internal sealed class BlockbenchImporterEditor : ScriptedImporterEditor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("importScale"),
                new GUIContent("Import Scale",
                    "Unity units generated for one Blockbench model unit. The default 0.0625 matches Blockbench's 16-units-per-meter export convention."));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("includeHiddenElements"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("includeNonExportedElements"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("importBoundingBoxesAsColliders"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("generateBackfacesForDoubleSidedTextures"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Textures & Materials", EditorStyles.boldLabel);
            SerializedProperty textures = serializedObject.FindProperty("importTextures");
            EditorGUILayout.PropertyField(textures);
            using (new EditorGUI.DisabledScope(!textures.boolValue))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("materialMode"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textureFilter"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textureWrap"));
                SerializedProperty alpha = serializedObject.FindProperty("alphaMode");
                EditorGUILayout.PropertyField(alpha);
                if ((BlockbenchAlphaMode)alpha.enumValueIndex != BlockbenchAlphaMode.Opaque)
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("alphaCutoff"));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation", EditorStyles.boldLabel);
            SerializedProperty animations = serializedObject.FindProperty("importAnimations");
            EditorGUILayout.PropertyField(animations);
            using (new EditorGUI.DisabledScope(!animations.boolValue))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("createAnimatorController"));

            EditorGUILayout.HelpBox(
                "The .bbmodel is the prefab. Embedded textures, materials, meshes, clips, and the optional Animator Controller are sub-assets.",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}
