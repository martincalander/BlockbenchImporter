using System;
using System.Collections.Generic;
using MartinCalander.BlockbenchImporter.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MartinCalander.BlockbenchImporter.Builders
{
    /// <summary>
    /// Builds textures and materials from Blockbench texture definitions.
    /// </summary>
    public class MaterialBuilder
    {
        private readonly BbModel model;
        private readonly string assetPath;
        private readonly BlockbenchImporterSettings settings;

        private readonly Dictionary<int, Texture2D> builtTextures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<int, Material> builtMaterials = new Dictionary<int, Material>();
        private Material defaultMaterial;

        public MaterialBuilder(BbModel model, string assetPath, BlockbenchImporterSettings settings)
        {
            this.model = model;
            this.assetPath = assetPath;
            this.settings = settings;
        }

        /// <summary>
        /// Build all textures from the model.
        /// </summary>
        public Dictionary<int, Texture2D> BuildTextures()
        {
            builtTextures.Clear();

            for (int i = 0; i < model.textures.Count; i++)
            {
                var texDef = model.textures[i];
                var texture = CreateTexture(texDef, i);
                if (texture != null)
                {
                    builtTextures[i] = texture;
                    texDef.unityTexture = texture;
                }
            }

            return builtTextures;
        }

        /// <summary>
        /// Build all materials from textures.
        /// </summary>
        public Dictionary<int, Material> BuildMaterials()
        {
            builtMaterials.Clear();

            foreach (var kvp in builtTextures)
            {
                var material = CreateMaterial(kvp.Value, model.textures[kvp.Key]);
                builtMaterials[kvp.Key] = material;
            }

            return builtMaterials;
        }

        /// <summary>
        /// Get material for a texture index.
        /// Returns default material if texture doesn't exist.
        /// </summary>
        public Material GetMaterial(int textureIndex)
        {
            if (builtMaterials.TryGetValue(textureIndex, out var mat))
                return mat;

            return GetDefaultMaterial();
        }

        /// <summary>
        /// Get materials array for a mesh's submeshes.
        /// </summary>
        public Material[] GetMaterialsForSubmeshes(int[] textureIndices)
        {
            var materials = new Material[textureIndices.Length];
            for (int i = 0; i < textureIndices.Length; i++)
            {
                materials[i] = GetMaterial(textureIndices[i]);
            }
            return materials;
        }

        /// <summary>
        /// Get or create a default material for faces without textures.
        /// </summary>
        public Material GetDefaultMaterial()
        {
            if (defaultMaterial != null)
                return defaultMaterial;

            defaultMaterial = CreateDefaultMaterial();
            return defaultMaterial;
        }

        /// <summary>
        /// Get all built textures.
        /// </summary>
        public IEnumerable<Texture2D> GetAllTextures()
        {
            return builtTextures.Values;
        }

        /// <summary>
        /// Get all built materials.
        /// </summary>
        public IEnumerable<Material> GetAllMaterials()
        {
            foreach (var mat in builtMaterials.Values)
                yield return mat;
            if (defaultMaterial != null)
                yield return defaultMaterial;
        }

        private Texture2D CreateTexture(BbTexture texDef, int index)
        {
            byte[] imageData = null;

            // Try to decode embedded base64 data
            if (!string.IsNullOrEmpty(texDef.source) && texDef.source.StartsWith("data:image"))
            {
                imageData = DecodeBase64Image(texDef.source);
            }
            // Try to load from external path
            else if (!string.IsNullOrEmpty(texDef.path))
            {
                imageData = LoadExternalTexture(texDef.path);
            }

            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogWarning($"Could not load texture data for: {texDef.name ?? $"Texture_{index}"}");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = SanitizeName(texDef.name ?? $"Texture_{index}");

            if (!texture.LoadImage(imageData))
            {
                Debug.LogError($"Failed to decode image data for texture: {texture.name}");
                return null;
            }

            // Apply pixel art settings
            texture.filterMode = settings.pixelArt ? FilterMode.Point : FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(false, false); // Don't make read-only so we can check alpha

            return texture;
        }

        private byte[] DecodeBase64Image(string dataUri)
        {
            try
            {
                // Format: data:image/png;base64,<data>
                int commaIndex = dataUri.IndexOf(',');
                if (commaIndex < 0)
                    return null;

                string base64 = dataUri.Substring(commaIndex + 1);
                return Convert.FromBase64String(base64);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to decode base64 image: {e.Message}");
                return null;
            }
        }

        private byte[] LoadExternalTexture(string path)
        {
            try
            {
                string directory = System.IO.Path.GetDirectoryName(assetPath);
                string fullPath = System.IO.Path.Combine(directory, path);

                if (System.IO.File.Exists(fullPath))
                {
                    return System.IO.File.ReadAllBytes(fullPath);
                }

                // Try absolute path
                if (System.IO.File.Exists(path))
                {
                    return System.IO.File.ReadAllBytes(path);
                }

                Debug.LogWarning($"External texture not found: {path}");
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load external texture: {e.Message}");
                return null;
            }
        }

        private Material CreateMaterial(Texture2D texture, BbTexture texDef)
        {
            Shader shader = GetShader(texDef);
            var material = new Material(shader);
            material.name = texture.name + "_Mat";

            // Set main texture
            material.mainTexture = texture;

            // Determine if we need alpha clipping
            bool needsAlphaClip = settings.forceAlphaClip || HasTransparency(texture);
            bool doubleSided = texDef.render_sides == "double";

            // Configure material based on render pipeline
            ConfigureMaterial(material, needsAlphaClip, doubleSided, texDef);

            return material;
        }

        private Material CreateDefaultMaterial()
        {
            Shader shader = GetDefaultShader();
            var material = new Material(shader);
            material.name = "Default_NoTexture";
            material.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            return material;
        }

        private Shader GetShader(BbTexture texDef)
        {
            // Try to detect render pipeline and use appropriate shader
            var pipeline = GraphicsSettings.currentRenderPipeline;

            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;

                // URP
                if (pipelineName.Contains("Universal") || pipelineName.Contains("URP"))
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader != null) return shader;

                    shader = Shader.Find("Universal Render Pipeline/Simple Lit");
                    if (shader != null) return shader;
                }

                // HDRP
                if (pipelineName.Contains("HD") || pipelineName.Contains("HDRP"))
                {
                    var shader = Shader.Find("HDRP/Lit");
                    if (shader != null) return shader;
                }
            }

            // Built-in pipeline
            return Shader.Find("Standard");
        }

        private Shader GetDefaultShader()
        {
            return GetShader(null);
        }

        private void ConfigureMaterial(Material material, bool needsAlphaClip, bool doubleSided, BbTexture texDef)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;

            if (pipeline != null)
            {
                string pipelineName = pipeline.GetType().Name;

                // URP configuration
                if (pipelineName.Contains("Universal") || pipelineName.Contains("URP"))
                {
                    ConfigureURPMaterial(material, needsAlphaClip, doubleSided);
                    return;
                }

                // HDRP configuration
                if (pipelineName.Contains("HD") || pipelineName.Contains("HDRP"))
                {
                    ConfigureHDRPMaterial(material, needsAlphaClip, doubleSided);
                    return;
                }
            }

            // Built-in pipeline configuration
            ConfigureBuiltInMaterial(material, needsAlphaClip, doubleSided);
        }

        private void ConfigureBuiltInMaterial(Material material, bool needsAlphaClip, bool doubleSided)
        {
            if (needsAlphaClip)
            {
                material.SetFloat("_Mode", 1); // Cutout mode
                material.SetInt("_SrcBlend", (int)BlendMode.One);
                material.SetInt("_DstBlend", (int)BlendMode.Zero);
                material.SetInt("_ZWrite", 1);
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetFloat("_Cutoff", settings.alphaCutoff);
            }

            if (doubleSided)
            {
                material.SetInt("_Cull", (int)CullMode.Off);
            }
        }

        private void ConfigureURPMaterial(Material material, bool needsAlphaClip, bool doubleSided)
        {
            if (needsAlphaClip)
            {
                material.SetFloat("_Surface", 0); // Opaque
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", settings.alphaCutoff);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }

            if (doubleSided)
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }
        }

        private void ConfigureHDRPMaterial(Material material, bool needsAlphaClip, bool doubleSided)
        {
            if (needsAlphaClip)
            {
                material.SetFloat("_SurfaceType", 0); // Opaque
                material.SetFloat("_AlphaCutoffEnable", 1);
                material.SetFloat("_AlphaCutoff", settings.alphaCutoff);
                material.EnableKeyword("_ALPHATEST_ON");
            }

            if (doubleSided)
            {
                material.SetFloat("_DoubleSidedEnable", 1);
                material.SetFloat("_CullMode", (float)CullMode.Off);
            }
        }

        private bool HasTransparency(Texture2D texture)
        {
            // Quick check for alpha by sampling some pixels
            if (!texture.isReadable)
                return false;

            try
            {
                var pixels = texture.GetPixels32();
                int sampleStep = Mathf.Max(1, pixels.Length / 100); // Sample about 100 pixels

                for (int i = 0; i < pixels.Length; i += sampleStep)
                {
                    if (pixels[i].a < 255)
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "Unnamed";

            // Remove invalid characters
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }

            return name;
        }
    }

    /// <summary>
    /// Settings for the BlockbenchImporter.
    /// </summary>
    [Serializable]
    public class BlockbenchImporterSettings
    {
        public float scale = 1f;
        public bool importAnimations = true;
        public bool createAnimatorController = true;
        public bool importTextures = true;
        public bool forceAlphaClip = true;
        public bool pixelArt = true;
        public float alphaCutoff = 0.5f;
        public UnitsMode unitsMode = UnitsMode.PixelsAre1Unit;

        public float GetUnitsScale()
        {
            switch (unitsMode)
            {
                case UnitsMode.PixelsAre1Unit:
                    return 1f / 16f; // 16 pixels = 1 unit
                case UnitsMode.SixteenPixelsPerUnit:
                    return 1f / 16f;
                case UnitsMode.Custom:
                    return scale;
                default:
                    return 1f / 16f;
            }
        }
    }

    public enum UnitsMode
    {
        PixelsAre1Unit,
        SixteenPixelsPerUnit,
        Custom
    }
}
