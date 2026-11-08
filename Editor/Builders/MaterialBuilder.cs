using System;
using System.Collections.Generic;
using System.Globalization;
using MartinCalander.BlockbenchImporter.Model;
using MartinCalander.BlockbenchImporter.Utils;
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
        private readonly Dictionary<int, Material> markerMaterials = new Dictionary<int, Material>();
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private readonly Dictionary<int, BlockbenchTextureAnimation.Flipbook> textureFlipbooks =
            new Dictionary<int, BlockbenchTextureAnimation.Flipbook>();
        private readonly Dictionary<Material, BlockbenchTextureAnimation.Flipbook> materialFlipbooks =
            new Dictionary<Material, BlockbenchTextureAnimation.Flipbook>();
        private readonly RenderPipelineKind pipeline;

        private enum RenderPipelineKind
        {
            BuiltIn,
            Universal,
            HighDefinition
        }
        private static readonly Color[] MarkerColors =
        {
            Html("A2EBFF"), Html("FFF899"), Html("F1BB75"), Html("FF9B97"), Html("C5A6E8"),
            Html("A6C8FF"), Html("7BFFA3"), Html("BDFFA6"), Html("FFA5D5"), Html("E0E9FB")
        };

        public MaterialBuilder(BbModel model, string assetPath, BlockbenchImporterSettings settings)
        {
            this.model = model;
            this.assetPath = assetPath;
            this.settings = settings;
            pipeline = DetectPipeline();
        }

        private static RenderPipelineKind DetectPipeline()
        {
            string name = GraphicsSettings.currentRenderPipeline?.GetType().Name;
            if (name == null) return RenderPipelineKind.BuiltIn;
            if (name.Contains("Universal") || name.Contains("URP")) return RenderPipelineKind.Universal;
            return name.Contains("HD") ? RenderPipelineKind.HighDefinition : RenderPipelineKind.BuiltIn;
        }

        private bool IsJavaFormat => model.meta?.model_format == "java_block";
        private bool IsBedrockFormat => model.meta?.model_format?.Contains("bedrock") == true;

        /// <summary>
        /// Build all textures from the model.
        /// </summary>
        public Dictionary<int, Texture2D> BuildTextures()
        {
            builtTextures.Clear();
            textureFlipbooks.Clear();
            if (!settings.importTextures) return builtTextures;

            for (int i = 0; i < model.textures.Count; i++)
            {
                var texDef = model.textures[i];
                var texture = CreateTexture(texDef, i);
                if (texture != null)
                {
                    builtTextures[i] = texture;
                    texDef.unityTexture = texture;
                    if (TryCreateFlipbook(texDef, texture.width, texture.height, IsJavaFormat, out var flipbook))
                    {
                        textureFlipbooks[i] = flipbook;
                        if (texDef.frame_interpolate)
                            model.warnings.Add("Animated texture '" + texture.name +
                                               "' blends frames in Blockbench. Unity plays its frames without blending.");
                    }
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
            materialFlipbooks.Clear();
            var warnedGroups = new HashSet<BbTextureGroup>();

            foreach (var kvp in builtTextures)
            {
                BbTexture texDef = model.textures[kvp.Key];
                BbTextureGroup group = GetMaterialGroup(texDef);
                // Normal, height, and MER textures feed their group's color material.
                if (group != null && texDef.pbr_channel != "color")
                    continue;
                Material material;
                if (group != null && settings.materialMode == BlockbenchMaterialMode.Lit &&
                    pipeline != RenderPipelineKind.HighDefinition)
                {
                    material = CreatePbrMaterial(kvp.Value, texDef, group);
                }
                else
                {
                    if (group != null && warnedGroups.Add(group))
                        model.warnings.Add("Texture group '" + (group.name ?? group.uuid) + "' is a PBR material. " +
                            (settings.materialMode == BlockbenchMaterialMode.Lit
                                ? "HDRP materials use only its color texture."
                                : "Set Material Mode to Lit to apply its normal, height, and MER textures."));
                    material = CreateMaterial(kvp.Value, texDef);
                }
                RegisterMaterial(kvp.Key, material);
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

            // A face can use a PBR channel texture directly; give it an ordinary material on demand.
            if (builtTextures.TryGetValue(textureIndex, out Texture2D channelTexture))
                return RegisterMaterial(textureIndex, CreateMaterial(channelTexture, model.textures[textureIndex]));

            return GetMarkerMaterial(Mathf.Max(0, -textureIndex - 1));
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
        public Material GetMarkerMaterial(int colorIndex)
        {
            colorIndex = Mathf.Abs(colorIndex) % MarkerColors.Length;
            if (!markerMaterials.TryGetValue(colorIndex, out Material material))
                markerMaterials[colorIndex] = material = CreateDefaultMaterial(colorIndex);
            return material;
        }

        /// <summary>
        /// Get all built textures.
        /// </summary>
        public IEnumerable<Texture2D> GetAllTextures()
        {
            foreach (Texture2D texture in builtTextures.Values)
                yield return texture;
            foreach (Texture2D texture in generatedTextures)
                yield return texture;
        }

        /// <summary>
        /// Get the flipbook playback data of a material created for an animated texture.
        /// </summary>
        internal bool TryGetFlipbook(Material material, out BlockbenchTextureAnimation.Flipbook flipbook) =>
            materialFlipbooks.TryGetValue(material, out flipbook);

        private Material RegisterMaterial(int textureIndex, Material material)
        {
            builtMaterials[textureIndex] = material;
            if (textureFlipbooks.TryGetValue(textureIndex, out var flipbook))
            {
                // Blockbench UVs address one frame; the material shows the first frame until playback starts.
                string property = GetMainTextureProperty(material);
                material.SetTextureScale(property, new Vector2(1f, 1f / flipbook.frameCount));
                material.SetTextureOffset(property, new Vector2(0f, 1f - 1f / flipbook.frameCount));
                flipbook.scaleOffsetProperty = property + "_ST";
                materialFlipbooks[material] = flipbook;
            }
            return material;
        }

        private static string GetMainTextureProperty(Material material)
        {
            Shader shader = material.shader;
            for (int index = 0; index < shader.GetPropertyCount(); index++)
                if ((shader.GetPropertyFlags(index) & ShaderPropertyFlags.MainTexture) != 0)
                    return shader.GetPropertyName(index);
            return material.HasProperty("_BaseMap") ? "_BaseMap" :
                material.HasProperty("_BaseColorMap") ? "_BaseColorMap" : "_MainTex";
        }

        private BbTextureGroup GetMaterialGroup(BbTexture texture)
        {
            if (string.IsNullOrEmpty(texture.group))
                return null;
            foreach (BbTextureGroup group in model.texture_groups)
                if (group.is_material && group.uuid == texture.group)
                    return group;
            return null;
        }

        /// <summary>
        /// Derive Blockbench's flipbook frame count and order from a texture strip whose aspect ratio differs
        /// from its UV size. Frames are stacked vertically with the first frame at the top.
        /// </summary>
        internal static bool TryCreateFlipbook(BbTexture texture, int width, int height, bool minecraftTiming,
            out BlockbenchTextureAnimation.Flipbook flipbook)
        {
            flipbook = default;
            if (width <= 0 || height <= 0 || texture.uv_width <= 0 || texture.uv_height <= 0)
                return false;
            float uvRatio = texture.uv_width / (float)texture.uv_height;
            float ratio = width / (float)height;
            if (Mathf.Approximately(uvRatio, ratio))
                return false;
            double frameCount = Math.Ceiling((double)texture.uv_width * height / texture.uv_height / width - 0.05);
            // Each vertical frame needs at least one image row. Check before converting or allocating.
            if (frameCount > height)
                throw new FormatException("Texture '" + (texture.name ?? "unnamed") +
                    "' has more animation frames than image rows. Check its UV width and height in Blockbench.");
            int count = (int)frameCount;
            if (count <= 1)
                return false;

            // Minecraft formats time frames in 20 Hz ticks; other formats use frames per second.
            float step = minecraftTiming
                ? Mathf.Max(16.66f, 50f * texture.frame_time) / 1000f
                : 1f / Mathf.Max(1f, texture.fps);
            var frames = new List<int>();
            var durations = new List<float>();
            switch (texture.frame_order_type)
            {
                case "backwards":
                    for (int frame = count - 1; frame >= 0; frame--)
                    {
                        frames.Add(frame);
                        durations.Add(step);
                    }
                    break;
                case "back_and_forth":
                    for (int index = 0; index < count * 2 - 2; index++)
                    {
                        frames.Add(index < count ? index : count * 2 - 2 - index);
                        durations.Add(step);
                    }
                    break;
                case "custom":
                    foreach (string token in (texture.frame_order ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string[] parts = token.Split(':');
                        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int frame))
                            continue;
                        frames.Add((frame % count + count) % count);
                        durations.Add(minecraftTiming && parts.Length > 1 &&
                                      int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ticks) &&
                                      ticks > 0
                            ? ticks * 0.05f
                            : step);
                    }
                    break;
            }
            if (frames.Count == 0)
            {
                for (int frame = 0; frame < count; frame++)
                {
                    frames.Add(frame);
                    durations.Add(step);
                }
            }

            flipbook = new BlockbenchTextureAnimation.Flipbook
            {
                frameCount = count,
                frames = frames.ToArray(),
                durations = durations.ToArray()
            };
            return true;
        }

        /// <summary>
        /// Get all built materials.
        /// </summary>
        public IEnumerable<Material> GetAllMaterials()
        {
            foreach (var mat in builtMaterials.Values)
                yield return mat;
            foreach (var material in markerMaterials.Values)
                yield return material;
        }

        private Texture2D CreateTexture(BbTexture texDef, int index)
        {
            byte[] imageData = null;

            // Blockbench prefers a current external image over the embedded snapshot.
            if (!string.IsNullOrEmpty(texDef.relative_path))
                imageData = LoadExternalTexture(texDef.relative_path);
            if ((imageData == null || imageData.Length == 0) && !string.IsNullOrEmpty(texDef.path))
                imageData = LoadExternalTexture(texDef.path);
            if ((imageData == null || imageData.Length == 0) && !string.IsNullOrEmpty(texDef.source) &&
                texDef.source.StartsWith("data:image", StringComparison.Ordinal))
            {
                imageData = DecodeBase64Image(texDef.source);
            }

            if (imageData == null || imageData.Length == 0)
            {
                Debug.LogWarning($"Could not load texture data for: {texDef.name ?? $"Texture_{index}"}");
                return null;
            }

            // Normal, height, and MER channels hold data rather than color, so they are sampled linearly.
            bool linear = texDef.pbr_channel == "normal" || texDef.pbr_channel == "height" || texDef.pbr_channel == "mer";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            try
            {
                texture.name = SanitizeName(texDef.name ?? $"Texture_{index}");

                if (!texture.LoadImage(imageData))
                {
                    Debug.LogError($"Failed to decode image data for texture: {texture.name}");
                    UnityEngine.Object.DestroyImmediate(texture);
                    return null;
                }

                // Apply pixel art settings
                texture.filterMode = settings.textureFilter;
                texture.wrapMode = settings.textureWrap;

                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
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
                path = path.Replace('\\', '/');
                string directory = System.IO.Path.GetDirectoryName(assetPath) ?? string.Empty;
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

                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load external texture: {e.Message}");
                return null;
            }
        }

        private Material CreateMaterial(Texture2D texture, BbTexture texDef, Shader shader = null)
        {
            var material = new Material(shader != null ? shader : GetShader());
            try
            {
                material.name = texture.name + "_Mat";

                // Set main texture
                material.mainTexture = texture;

                // Determine if we need alpha clipping
                bool needsAlphaClip = settings.alphaMode == BlockbenchAlphaMode.Cutout ||
                                      (settings.alphaMode == BlockbenchAlphaMode.Auto && HasTransparency(texture));
                CullMode cullMode = texDef.render_sides == "back" ? CullMode.Front :
                    texDef.render_sides == "double" || texDef.render_sides == "auto" ? CullMode.Off : CullMode.Back;

                // Configure material based on render pipeline
                ConfigureMaterial(material, needsAlphaClip, cullMode);

                return material;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(material);
                throw;
            }
        }

        private Material CreateDefaultMaterial(int colorIndex)
        {
            Shader shader = GetShader();
            var material = new Material(shader);
            try
            {
                material.name = "Untextured_" + colorIndex;
                material.color = MarkerColors[colorIndex];
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", MarkerColors[colorIndex]);
                return material;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(material);
                throw;
            }
        }

        private static Color Html(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }

        private Shader GetShader()
        {
            bool unlit = settings.materialMode == BlockbenchMaterialMode.Unlit;
            if (pipeline == RenderPipelineKind.Universal)
            {
                Shader shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit") ??
                                Shader.Find("Universal Render Pipeline/Simple Lit");
                if (shader != null) return shader;
            }
            else if (pipeline == RenderPipelineKind.HighDefinition)
            {
                Shader shader = Shader.Find(unlit ? "HDRP/Unlit" : "HDRP/Lit");
                if (shader != null) return shader;
            }

            // Built-in pipeline
            return Shader.Find(unlit ? "Blockbench/Unlit" : "Blockbench/Lit") ?? Shader.Find("Standard");
        }

        private void ConfigureMaterial(Material material, bool needsAlphaClip, CullMode cullMode)
        {
            if (pipeline == RenderPipelineKind.Universal)
                ConfigureURPMaterial(material, needsAlphaClip, cullMode);
            else if (pipeline == RenderPipelineKind.HighDefinition)
                ConfigureHDRPMaterial(material, needsAlphaClip, cullMode);
            else
                ConfigureBuiltInMaterial(material, needsAlphaClip, cullMode);
        }

        /// <summary>
        /// Create a lit material from a Blockbench material texture group: color, normal or height, and MER.
        /// MER stores metalness in red, an emissive mask in green, and roughness in blue.
        /// </summary>
        private Material CreatePbrMaterial(Texture2D color, BbTexture texDef, BbTextureGroup group)
        {
            bool universal = pipeline == RenderPipelineKind.Universal;
            Shader shader = Shader.Find(universal ? "Universal Render Pipeline/Lit" : "Standard");
            Material material = CreateMaterial(color, texDef, shader);
            try
            {
                Texture2D normal = FindGroupTexture(group, "normal");
                Texture2D height = FindGroupTexture(group, "height");
                Texture2D mer = FindGroupTexture(group, "mer");

                // Bedrock uses DirectX normal maps; Unity expects the green channel pointing up.
                Texture2D normalMap = normal != null
                    ? IsBedrockFormat ? Generate(normal, "_Normal", true, FlipGreen(normal)) : normal
                    : height != null ? Generate(height, "_Normal", true, NormalsFromHeight(height)) : null;
                if (normalMap != null)
                {
                    material.SetTexture("_BumpMap", normalMap);
                    material.SetFloat("_BumpScale", 1f);
                    material.EnableKeyword("_NORMALMAP");
                }

                if (mer != null)
                {
                    material.SetTexture("_MetallicGlossMap", Generate(mer, "_MetallicSmoothness", true, MetallicSmoothness(mer)));
                    material.EnableKeyword(universal ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
                    material.SetFloat("_Metallic", 1f);
                    material.SetFloat(universal ? "_Smoothness" : "_GlossMapScale", 1f);
                    Color32[] emission = Emission(color, mer);
                    if (emission != null)
                        SetEmission(material, Generate(mer, "_Emission", false, emission), Color.white);
                }
                else
                {
                    material.SetFloat("_Metallic", Channel(group.mer_value, 0));
                    material.SetFloat(universal ? "_Smoothness" : "_Glossiness", 1f - Channel(group.mer_value, 2));
                    float emissive = Channel(group.mer_value, 1);
                    if (emissive > 0f)
                        SetEmission(material, color, new Color(emissive, emissive, emissive));
                }
                return material;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(material);
                throw;
            }
        }

        private Texture2D FindGroupTexture(BbTextureGroup group, string channel)
        {
            for (int index = 0; index < model.textures.Count; index++)
                if (model.textures[index].group == group.uuid && model.textures[index].pbr_channel == channel &&
                    builtTextures.TryGetValue(index, out Texture2D texture))
                    return texture;
            return null;
        }

        private static float Channel(float[] values, int index) =>
            values != null && index < values.Length ? Mathf.Clamp01(values[index] / 255f) : 0f;

        private static void SetEmission(Material material, Texture texture, Color color)
        {
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", color);
            material.EnableKeyword("_EMISSION");
        }

        private Texture2D Generate(Texture2D source, string suffix, bool linear, Color32[] pixels)
        {
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, linear)
            {
                name = source.name + suffix,
                filterMode = settings.textureFilter,
                wrapMode = settings.textureWrap
            };
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                generatedTextures.Add(texture);
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        internal static Color32[] FlipGreen(Texture2D normal)
        {
            Color32[] pixels = normal.GetPixels32();
            for (int index = 0; index < pixels.Length; index++)
                pixels[index].g = (byte)(255 - pixels[index].g);
            return pixels;
        }

        /// <summary>Convert a grayscale height map to a tangent-space normal map from neighbouring texel slopes.</summary>
        internal static Color32[] NormalsFromHeight(Texture2D height)
        {
            Color32[] source = height.GetPixels32();
            int width = height.width;
            int rows = height.height;
            var normals = new Color32[source.Length];
            float Sample(int x, int y) => source[Mathf.Clamp(y, 0, rows - 1) * width + Mathf.Clamp(x, 0, width - 1)].r / 255f;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < width; x++)
            {
                Vector3 normal = new Vector3(Sample(x - 1, y) - Sample(x + 1, y), Sample(x, y - 1) - Sample(x, y + 1), 1f).normalized;
                normals[y * width + x] = new Color32(
                    (byte)Mathf.RoundToInt((normal.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.y * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((normal.z * 0.5f + 0.5f) * 255f), 255);
            }
            return normals;
        }

        /// <summary>Pack MER metalness into red and inverted roughness into alpha, as Unity metallic maps expect.</summary>
        internal static Color32[] MetallicSmoothness(Texture2D mer)
        {
            Color32[] pixels = mer.GetPixels32();
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = new Color32(pixels[index].r, 0, 0, (byte)(255 - pixels[index].b));
            return pixels;
        }

        /// <summary>Mask the color texture with the MER emissive channel, or return null when nothing is emissive.</summary>
        internal static Color32[] Emission(Texture2D color, Texture2D mer)
        {
            Color32[] mask = mer.GetPixels32();
            Color32[] colors = color.GetPixels32();
            var emission = new Color32[mask.Length];
            bool emissive = false;
            for (int y = 0; y < mer.height; y++)
            for (int x = 0; x < mer.width; x++)
            {
                int level = mask[y * mer.width + x].g;
                emissive |= level != 0;
                // Sample the nearest color texel so different texture sizes stay aligned.
                Color32 source = colors[y * color.height / mer.height * color.width + x * color.width / mer.width];
                emission[y * mer.width + x] = new Color32((byte)(source.r * level / 255), (byte)(source.g * level / 255),
                    (byte)(source.b * level / 255), 255);
            }
            return emissive ? emission : null;
        }

        private void ConfigureBuiltInMaterial(Material material, bool needsAlphaClip, CullMode cullMode)
        {
            if (needsAlphaClip)
            {
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 1); // Standard shader cutout mode
                    material.SetInt("_SrcBlend", (int)BlendMode.One);
                    material.SetInt("_DstBlend", (int)BlendMode.Zero);
                    material.SetInt("_ZWrite", 1);
                }
                material.EnableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.SetFloat("_Cutoff", settings.alphaCutoff);
            }

            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)cullMode);
            material.doubleSidedGI = cullMode == CullMode.Off;
        }

        private void ConfigureURPMaterial(Material material, bool needsAlphaClip, CullMode cullMode)
        {
            if (needsAlphaClip)
            {
                material.SetFloat("_Surface", 0); // Opaque
                material.SetFloat("_AlphaClip", 1);
                material.SetFloat("_Cutoff", settings.alphaCutoff);
                material.EnableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.SetOverrideTag("RenderType", "TransparentCutout");
            }

            material.SetFloat("_Cull", (float)cullMode);
            material.doubleSidedGI = cullMode == CullMode.Off;
        }

        private void ConfigureHDRPMaterial(Material material, bool needsAlphaClip, CullMode cullMode)
        {
            if (needsAlphaClip)
            {
                material.SetFloat("_SurfaceType", 0); // Opaque
                material.SetFloat("_AlphaCutoffEnable", 1);
                material.SetFloat("_AlphaCutoff", settings.alphaCutoff);
                material.EnableKeyword("_ALPHATEST_ON");
            }

            material.SetFloat("_DoubleSidedEnable", cullMode == CullMode.Off ? 1 : 0);
            material.SetFloat("_CullMode", (float)cullMode);
            if (material.HasProperty("_CullModeForward"))
                material.SetFloat("_CullModeForward", (float)cullMode);
            material.doubleSidedGI = cullMode == CullMode.Off;
        }

        private bool HasTransparency(Texture2D texture)
        {
            // A single transparent pixel is enough to require clipping.
            if (!texture.isReadable)
                return false;

            try
            {
                if (texture.format == TextureFormat.RGB24)
                    return false;

                if (texture.format == TextureFormat.RGBA32)
                {
                    // This view belongs to the texture and is only valid until its data changes.
                    var data = texture.GetPixelData<Color32>(0);
                    for (int index = 0; index < data.Length; index++)
                        if (data[index].a < 255)
                            return true;
                    return false;
                }

                var pixels = texture.GetPixels32();
                for (int i = 0; i < pixels.Length; i++)
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

        public bool IsDoubleSidedTexture(int textureIndex)
        {
            if (textureIndex < 0 || textureIndex >= model.textures.Count) return false;
            string sides = model.textures[textureIndex].render_sides;
            return sides == "double" || sides == "auto";
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
        public float importScale = CoordinateConverter.DefaultImportScale;
        public bool importAnimations = true;
        public bool createAnimatorController = true;
        public bool importEffectEvents = true;
        public bool importTextures = true;
        public bool animateTextures = true;
        public bool includeHiddenElements;
        public bool includeNonExportedElements;
        public bool importBoundingBoxesAsColliders = true;
        public bool generateBackfacesForDoubleSidedTextures;
        public BlockbenchMaterialMode materialMode = BlockbenchMaterialMode.Unlit;
        public FilterMode textureFilter = FilterMode.Point;
        public TextureWrapMode textureWrap = TextureWrapMode.Clamp;
        public BlockbenchAlphaMode alphaMode = BlockbenchAlphaMode.Auto;
        public float alphaCutoff = 0.5f;
    }

    public enum BlockbenchMaterialMode
    {
        Unlit,
        Lit
    }

    public enum BlockbenchAlphaMode
    {
        Auto,
        Opaque,
        Cutout
    }
}
