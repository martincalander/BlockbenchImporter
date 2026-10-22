using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MartinCalander.BlockbenchImporter.Builders;
using MartinCalander.BlockbenchImporter.Model;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchMaterialTests
    {
        private string directory;
        private readonly List<MaterialBuilder> builders = new List<MaterialBuilder>();
        private readonly List<Object> objects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BlockbenchMaterials_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MaterialBuilder builder in builders)
            {
                foreach (Material material in builder.GetAllMaterials())
                    Object.DestroyImmediate(material);
                foreach (Texture2D texture in builder.GetAllTextures())
                    Object.DestroyImmediate(texture);
            }
            builders.Clear();
            foreach (Object value in objects)
                Object.DestroyImmediate(value);
            objects.Clear();
            Directory.Delete(directory, true);
        }

        [TestCase(true, true, "#FF0000")]
        [TestCase(false, true, "#0000FF")]
        [TestCase(false, false, "#00FF00")]
        public void TextureSelectionPrefersRelativeThenAbsoluteThenEmbeddedImages(
            bool relativeExists, bool absoluteExists, string expectedColor)
        {
            Directory.CreateDirectory(Path.Combine(directory, "textures"));
            if (relativeExists)
                File.WriteAllBytes(Path.Combine(directory, "textures", "current.png"), Png(Color.red));
            string absolutePath = Path.Combine(directory, "current.png");
            if (absoluteExists) File.WriteAllBytes(absolutePath, Png(Color.blue));
            MaterialBuilder builder = Builder(new BbTexture
            {
                relative_path = "textures\\current.png",
                path = absolutePath,
                source = DataUri(Color.green)
            });
            ColorUtility.TryParseHtmlString(expectedColor, out Color expected);
            AssertColor(builder.BuildTextures()[0].GetPixel(0, 0), expected);
        }

        [TestCase(BlockbenchAlphaMode.Auto, true)]
        [TestCase(BlockbenchAlphaMode.Cutout, true)]
        [TestCase(BlockbenchAlphaMode.Opaque, false)]
        public void IsolatedTransparentPixelSelectsCorrectAlphaMode(BlockbenchAlphaMode mode, bool clip)
        {
            var source = Keep(new Texture2D(20, 20, TextureFormat.RGBA32, false));
            var pixels = new Color32[400];
            for (int index = 0; index < pixels.Length; index++)
                pixels[index] = new Color32(255, 255, 255, 255);
            pixels[1].a = 0;
            source.SetPixels32(pixels);
            source.Apply();
            MaterialBuilder builder = Builder(new BbTexture
            {
                source = "data:image/png;base64," + Convert.ToBase64String(source.EncodeToPNG())
            }, new BlockbenchImporterSettings { alphaMode = mode });
            Texture2D imported = builder.BuildTextures()[0];
            Material material = builder.BuildMaterials()[0];

            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.EqualTo(clip));
            Assert.That(material.mainTexture, Is.SameAs(imported));
        }

        [TestCase("front", CullMode.Back)]
        [TestCase("back", CullMode.Front)]
        [TestCase("double", CullMode.Off)]
        [TestCase("auto", CullMode.Off)]
        public void GeneratedUnlitMaterialPreservesTextureSides(string sides, CullMode expected)
        {
            MaterialBuilder builder = Builder(new BbTexture { source = DataUri(Color.white), render_sides = sides });
            builder.BuildTextures();
            Material material = builder.BuildMaterials()[0];
            string property = material.HasProperty("_Cull") ? "_Cull" : "_CullMode";

            Assert.That(material.HasProperty(property), Is.True);
            Assert.That(material.GetInt(property), Is.EqualTo((int)expected));
        }

        [Test]
        public void GeneratedMarkerMaterialRetainsItsColor()
        {
            Material material = Builder(null).GetMarkerMaterial(0);
            ColorUtility.TryParseHtmlString("#A2EBFF", out Color expected);

            AssertColor(material.color, expected);
        }

        [TestCase("ConfigureBuiltInMaterial", "Blockbench/Unlit")]
        [TestCase("ConfigureBuiltInMaterial", "Blockbench/Lit")]
        [TestCase("ConfigureURPMaterial", "Universal Render Pipeline/Unlit")]
        [TestCase("ConfigureURPMaterial", "Universal Render Pipeline/Lit")]
        public void PipelineMaterialsUseCutoutQueueAndRequestedCulling(string method, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                Assert.Ignore("Shader is not installed: " + shaderName);
            var material = Keep(new Material(shader));
            Configure(Builder(null), method, material, true, CullMode.Front);

            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.True);
            Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.5f));
            Assert.That(material.GetInt("_Cull"), Is.EqualTo((int)CullMode.Front));
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
            Assert.That(material.GetTag("RenderType", false), Is.EqualTo("TransparentCutout"));
        }

        [TestCase("Blockbench/Unlit")]
        [TestCase("Blockbench/Lit")]
        public void BuiltInShadersRenderMarkerColorAndClipTransparentPixels(string shaderName)
        {
            RequireGraphics();
            Shader shader = Shader.Find(shaderName);
            Assert.That(shader, Is.Not.Null);
            var material = Keep(new Material(shader));
            material.color = Color.red;
            var texture = Keep(new Texture2D(2, 1, TextureFormat.RGBA32, false));
            texture.SetPixels(new[] { Color.white, Color.clear });
            texture.Apply();
            texture.filterMode = FilterMode.Point;
            material.mainTexture = texture;
            Configure(Builder(null), "ConfigureBuiltInMaterial", material, true, CullMode.Off);

            Color[] clipped = Render(material, false);
            AssertColor(clipped[8 * 16 + 3], Color.red);
            AssertColor(clipped[8 * 16 + 12], Color.magenta);

            material.DisableKeyword("_ALPHATEST_ON");
            Color[] opaque = Render(material, false);
            AssertColor(opaque[8 * 16 + 12], Color.black);
        }

        [TestCase("Blockbench/Unlit")]
        [TestCase("Blockbench/Lit")]
        public void BuiltInShadersRenderBothWindingsOnlyWhenCullingIsDisabled(string shaderName)
        {
            RequireGraphics();
            var material = Keep(new Material(Shader.Find(shaderName)));
            material.color = Color.green;
            Configure(Builder(null), "ConfigureBuiltInMaterial", material, false, CullMode.Off);
            AssertColor(Render(material, false)[8 * 16 + 8], Color.green);
            AssertColor(Render(material, true)[8 * 16 + 8], Color.green);

            material.SetInt("_Cull", (int)CullMode.Back);
            Color forward = Render(material, false)[8 * 16 + 8];
            Color reverse = Render(material, true)[8 * 16 + 8];
            Assert.That((forward - reverse).maxColorComponent, Is.GreaterThan(0.5f),
                "One winding should be drawn while the opposite winding is culled.");
            material.SetInt("_Cull", (int)CullMode.Front);
            AssertColor(Render(material, false)[8 * 16 + 8], reverse);
            AssertColor(Render(material, true)[8 * 16 + 8], forward);
        }

        private MaterialBuilder Builder(BbTexture texture, BlockbenchImporterSettings settings = null)
        {
            var model = new BbModel();
            if (texture != null)
            {
                texture.name = "TestTexture";
                model.textures.Add(texture);
            }
            var builder = new MaterialBuilder(model, Path.Combine(directory, "Model.bbmodel"),
                settings ?? new BlockbenchImporterSettings());
            builders.Add(builder);
            return builder;
        }

        private static void Configure(MaterialBuilder builder, string method, Material material, bool clip, CullMode cull)
        {
            typeof(MaterialBuilder).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(builder, new object[] { material, clip, cull });
        }

        private T Keep<T>(T value) where T : Object
        {
            objects.Add(value);
            return value;
        }

        private static string DataUri(Color color)
        {
            return "data:image/png;base64," + Convert.ToBase64String(Png(color));
        }

        private static byte[] Png(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixel(0, 0, color);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Rendering assertions need an Editor with a graphics device.");
        }

        private static Color[] Render(Material material, bool reverse)
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(-1, -1, 0.5f), new Vector3(1, -1, 0.5f),
                    new Vector3(1, 1, 0.5f), new Vector3(-1, 1, 0.5f)
                },
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up },
                triangles = reverse ? new[] { 0, 2, 1, 0, 3, 2 } : new[] { 0, 1, 2, 0, 2, 3 }
            };
            var target = new RenderTexture(16, 16, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(16, 16, TextureFormat.RGBA32, false, true);
            var commands = new CommandBuffer();
            RenderTexture previous = RenderTexture.active;
            string[] lightingGlobals =
            {
                "unity_SHAr", "unity_SHAg", "unity_SHAb", "unity_SHBr", "unity_SHBg", "unity_SHBb", "unity_SHC",
                "_LightColor0", "_WorldSpaceLightPos0"
            };
            var previousLighting = new Vector4[lightingGlobals.Length];
            try
            {
                mesh.RecalculateNormals();
                target.Create();
                commands.SetRenderTarget(target);
                commands.ClearRenderTarget(true, true, Color.magenta);
                commands.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                // A white ambient probe makes Lit and Unlit colors comparable without scene lights.
                for (int index = 0; index < lightingGlobals.Length; index++)
                {
                    previousLighting[index] = Shader.GetGlobalVector(lightingGlobals[index]);
                    Vector4 value = index < 3 ? new Vector4(0, 0, 0, 1) : Vector4.zero;
                    if (index == lightingGlobals.Length - 1) value = new Vector4(0, 0, 1, 0);
                    commands.SetGlobalVector(lightingGlobals[index], value);
                }
                commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0);
                for (int index = 0; index < lightingGlobals.Length; index++)
                    commands.SetGlobalVector(lightingGlobals[index], previousLighting[index]);
                Graphics.ExecuteCommandBuffer(commands);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                RenderTexture.active = previous;
                commands.Dispose();
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(mesh);
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.02f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.02f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.02f));
        }
    }
}
