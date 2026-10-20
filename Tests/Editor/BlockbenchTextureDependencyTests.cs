using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MartinCalander.BlockbenchImporter.Parsing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchTextureDependencyTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            string name = "__BlockbenchTextureTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            folder = "Assets/" + name;
            AssetDatabase.CreateFolder(folder, "Textures");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
        }

        [TestCase("Textures/pixel.png")]
        [TestCase("Textures\\pixel.png")]
        [TestCase("project-relative")]
        public void ExternalTextureCreationEditAndDeletionReimportTheModel(string relativePath)
        {
            string modelPath = folder + "/Dependency.bbmodel";
            string texturePath = folder + "/Textures/pixel.png";
            if (relativePath == "project-relative") relativePath = texturePath;
            var model = new Dictionary<string, object>
            {
                ["meta"] = new Dictionary<string, object>
                {
                    ["format_version"] = "5.0", ["model_format"] = "free"
                },
                ["textures"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["name"] = "pixel", ["uuid"] = "pixel",
                        ["relative_path"] = relativePath,
                        ["source"] = "data:image/png;base64," + Convert.ToBase64String(Png(Color.red))
                    }
                }
            };
            string source = MiniJson.Serialize(model);
            File.WriteAllText(modelPath, source);
            Refresh();
            AssertPixel(modelPath, Color.red);
            long originalId = TextureId(modelPath);

            File.WriteAllBytes(texturePath, Png(Color.green));
            Refresh();
            AssertPixel(modelPath, Color.green);
            Assert.That(TextureId(modelPath), Is.EqualTo(originalId));

            File.WriteAllBytes(texturePath, Png(Color.blue));
            Refresh();
            AssertPixel(modelPath, Color.blue);
            Assert.That(TextureId(modelPath), Is.EqualTo(originalId));

            AssetDatabase.DeleteAsset(texturePath);
            Refresh();
            AssertPixel(modelPath, Color.red);
            Assert.That(TextureId(modelPath), Is.EqualTo(originalId));
            Assert.That(File.ReadAllText(modelPath), Is.EqualTo(source),
                "Import and dependency changes must not rewrite the Blockbench project.");
        }

        private static void Refresh()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void AssertPixel(string path, Color expected)
        {
            Texture2D texture = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().Single();
            Color actual = texture.GetPixel(0, 0);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1f / 255f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1f / 255f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1f / 255f));
        }

        private static long TextureId(string path)
        {
            Texture2D texture = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().Single();
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string _, out long id),
                Is.True);
            return id;
        }

        private static byte[] Png(Color color)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels(new[] { color, color, color, color });
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
