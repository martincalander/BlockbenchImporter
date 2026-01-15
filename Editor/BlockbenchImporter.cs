using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter
{
    /// <summary>
    /// Unity ScriptedImporter for Blockbench (.bbmodel) files.
    /// Automatically imports Blockbench models as Unity meshes.
    /// </summary>
    [ScriptedImporter(2, "bbmodel")]
    public class BlockbenchImporter : ScriptedImporter
    {
        [Tooltip("Scale factor to apply to the imported model")]
        public float scaleFactor = 1f;
        
        [Tooltip("Generate a default material for the model")]
        public bool generateMaterial = true;
        
        [Tooltip("Default material color")]
        public Color defaultColor = Color.white;
        
        [Tooltip("Import animations from the model")]
        public bool importAnimations = true;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            try
            {
                string json = File.ReadAllText(ctx.assetPath);
                var model = BlockbenchJsonParser.Parse(json);
                
                if (model == null)
                {
                    ctx.LogImportError("Failed to parse Blockbench model");
                    return;
                }
                
                // Build the mesh with submeshes
                var meshResult = BlockbenchMeshBuilder.BuildMeshWithSubmeshes(model);
                Mesh mesh = meshResult.Mesh;
                
                if (mesh == null || mesh.vertexCount == 0)
                {
                    ctx.LogImportWarning("Blockbench model has no geometry");
                    mesh = new Mesh { name = model.name ?? "EmptyModel" };
                }
                
                // Apply scale factor
                if (Mathf.Abs(scaleFactor - 1f) > 0.0001f && mesh.vertexCount > 0)
                {
                    Vector3[] vertices = mesh.vertices;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] *= scaleFactor;
                    }
                    mesh.vertices = vertices;
                    mesh.RecalculateBounds();
                }
                
                // Create the root GameObject
                var root = new GameObject(model.name ?? Path.GetFileNameWithoutExtension(ctx.assetPath));
                
                // Add mesh filter and renderer
                var meshFilter = root.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                
                var meshRenderer = root.AddComponent<MeshRenderer>();
                
                // Create textures and materials for all embedded textures
                var texturesByIndex = new Dictionary<int, Texture2D>();
                var materialsByIndex = new Dictionary<int, Material>();
                
                if (model.textures != null)
                {
                    for (int i = 0; i < model.textures.Count; i++)
                    {
                        var texData = model.textures[i];
                        if (!string.IsNullOrEmpty(texData.source))
                        {
                            Texture2D texture = CreateTextureFromBase64(texData.source, texData.name);
                            if (texture != null)
                            {
                                string textureName = !string.IsNullOrEmpty(texData.name) ? texData.name : $"Texture_{i}";
                                texture.name = textureName;
                                ctx.AddObjectToAsset($"texture_{i}_{textureName}", texture);
                                texturesByIndex[i] = texture;
                                
                                // Create material for this texture
                                Material material = CreateMaterial(texture);
                                material.name = $"Material_{textureName}";
                                ctx.AddObjectToAsset($"material_{i}_{textureName}", material);
                                materialsByIndex[i] = material;
                            }
                        }
                    }
                }
                
                // Create default material for faces without texture
                Material defaultMaterial = null;
                if (generateMaterial)
                {
                    defaultMaterial = CreateDefaultMaterial(defaultColor);
                    defaultMaterial.name = "Material_Default";
                    ctx.AddObjectToAsset("material_default", defaultMaterial);
                    materialsByIndex[-1] = defaultMaterial;
                }
                
                // Assign materials to submeshes
                var materials = new List<Material>();
                for (int submeshIdx = 0; submeshIdx < meshResult.TextureIndicesPerSubmesh.Length; submeshIdx++)
                {
                    int textureIndex = meshResult.TextureIndicesPerSubmesh[submeshIdx];
                    
                    if (materialsByIndex.TryGetValue(textureIndex, out Material mat))
                    {
                        materials.Add(mat);
                    }
                    else if (defaultMaterial != null)
                    {
                        materials.Add(defaultMaterial);
                    }
                    else
                    {
                        // Create a fallback material
                        Material fallback = CreateDefaultMaterial(Color.magenta);
                        fallback.name = $"Material_Fallback_{submeshIdx}";
                        ctx.AddObjectToAsset($"material_fallback_{submeshIdx}", fallback);
                        materials.Add(fallback);
                    }
                }
                
                meshRenderer.sharedMaterials = materials.ToArray();
                
                // Import animations
                if (importAnimations && model.animations != null && model.animations.Count > 0)
                {
                    var animResult = BlockbenchAnimationBuilder.BuildAnimations(model, scaleFactor);
                    
                    foreach (var clip in animResult.Clips)
                    {
                        if (clip != null)
                        {
                            ctx.AddObjectToAsset($"animation_{clip.name}", clip);
                        }
                    }
                    
                    // If there are animations, add an Animator component to the root
                    if (animResult.Clips.Count > 0)
                    {
                        var animator = root.AddComponent<Animator>();
                        // Note: An AnimatorController would need to be created separately
                        // or assigned manually after import
                    }
                }
                
                // Add assets to context
                ctx.AddObjectToAsset("mesh", mesh);
                ctx.AddObjectToAsset("root", root);
                ctx.SetMainObject(root);
            }
            catch (Exception e)
            {
                ctx.LogImportError($"Error importing Blockbench model: {e.Message}\n{e.StackTrace}");
            }
        }
        
        private Texture2D CreateTextureFromBase64(string source, string textureName)
        {
            try
            {
                // Remove data URL prefix if present
                string base64Data = source;
                if (source.Contains(","))
                {
                    base64Data = source.Substring(source.IndexOf(',') + 1);
                }
                
                byte[] imageData = Convert.FromBase64String(base64Data);
                
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                
                if (texture.LoadImage(imageData))
                {
                    texture.Apply();
                    return texture;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load embedded texture '{textureName}': {e.Message}");
            }
            
            return null;
        }
        
        private Material CreateMaterial(Texture2D texture)
        {
            // Try to use URP/HDRP lit shader, fallback to standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            
            var material = new Material(shader);
            
            if (texture != null)
            {
                // Set main texture
                material.mainTexture = texture;
            }
            
            return material;
        }
        
        private Material CreateDefaultMaterial(Color color)
        {
            // Try to use URP/HDRP lit shader, fallback to standard
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("HDRP/Lit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            
            var material = new Material(shader);
            material.color = color;
            
            return material;
        }
    }
}
