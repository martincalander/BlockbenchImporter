using System.Collections.Generic;
using System.IO;
using System.Linq;
using MartinCalander.BlockbenchImporter.Utils;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Animations;
using UnityEngine;

namespace MartinCalander.BlockbenchImporter.Tests
{
    public sealed class BlockbenchImporterIntegrationTests
    {
        private const string Folder = "Assets/__BlockbenchImporterTests";
        private const string AssetPath = Folder + "/Integration.bbmodel";
        private const string ArmaturePath = Folder + "/Armature.bbmodel";
        private const string Model = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Integration Model"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[{""name"":""body"",""type"":""cube"",""uuid"":""cube"",""from"":[-1,0,-1],""to"":[1,2,1],""origin"":[0,1,0],""faces"":{""north"":{""uv"":[0,0,2,2],""texture"":0},""south"":{""uv"":[0,0,2,2],""texture"":0}}},{""name"":""hitbox"",""type"":""bounding_box"",""uuid"":""hitbox"",""from"":[-2,0,-2],""to"":[2,4,2],""origin"":[0,2,0]}],
          ""groups"":[{""name"":""root"",""uuid"":""group"",""origin"":[0,0,0]}],
          ""outliner"":[{""name"":""root"",""uuid"":""group"",""children"":[""cube"",""hitbox""]}],
          ""textures"":[{""name"":""orange"",""id"":""0"",""uuid"":""texture"",""width"":16,""height"":16,""uv_width"":16,""uv_height"":16,""render_sides"":""double"",""source"":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAK0lEQVR4AeyTsQkAAAzCgsf2/w8svcGhi4KrQzDy4KQiTAegDMrgNPr/wQIAAP//u+MxzwAAAAZJREFUAwDd+iZhI2QuxgAAAABJRU5ErkJggg==""}],
          ""animations"":[{""name"":""turn"",""loop"":""loop"",""length"":1,""snapping"":24,""animators"":{""group"":{""name"":""root"",""type"":""bone"",""keyframes"":[{""channel"":""rotation"",""time"":0,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""0"",""z"":""0""}]},{""channel"":""rotation"",""time"":1,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""90"",""z"":""0""}]},{""channel"":""position"",""time"":0,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""0"",""z"":""0""}]},{""channel"":""position"",""time"":1,""interpolation"":""linear"",""data_points"":[{""x"":""0"",""y"":""16"",""z"":""0""}]}]}}}]
        }";
        private const string ArmatureModel = @"{
          ""meta"":{""format_version"":""5.0"",""model_format"":""free"",""box_uv"":false},
          ""name"":""Armature"",""resolution"":{""width"":16,""height"":16},
          ""elements"":[
            {""name"":""rig"",""type"":""armature"",""uuid"":""armature"",""children"":[""bone"",""mesh0001""]},
            {""name"":""bone"",""type"":""armature_bone"",""uuid"":""bone"",""origin"":[0,0,0],""rotation"":[0,0,0],""children"":[],""vertex_weights"":{""a"":1,""b"":1,""c"":1}},
            {""name"":""triangle"",""type"":""mesh"",""uuid"":""mesh0001"",""origin"":[0,0,0],""vertices"":{""a"":[0,0,0],""b"":[1,0,0],""c"":[0,1,0]},""faces"":{""face"":{""vertices"":[""a"",""b"",""c""],""uv"":{""a"":[0,0],""b"":[1,0],""c"":[0,1]},""texture"":0}}}
          ],
          ""outliner"":[""armature""],
          ""textures"":[{""name"":""orange"",""id"":""0"",""uuid"":""texture"",""width"":16,""height"":16,""uv_width"":16,""uv_height"":16,""source"":""data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAAK0lEQVR4AeyTsQkAAAzCgsf2/w8svcGhi4KrQzDy4KQiTAegDMrgNPr/wQIAAP//u+MxzwAAAAZJREFUAwDd+iZhI2QuxgAAAABJRU5ErkJggg==""}]
        }";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__BlockbenchImporterTests");
            File.WriteAllText(AssetPath, Model);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(Folder);

        [Test]
        public void Import_CreatesPrefabAndAllConfiguredSubAssets()
        {
            var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);

            Assert.That(root, Is.Not.Null);
            // Unity names a scripted importer's main object after the source file.
            Assert.That(root.name, Is.EqualTo("Integration"));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Transform body = root.transform.Find("root/body");
            Assert.That(body, Is.Not.Null);
            Assert.That(body.localPosition.y, Is.EqualTo(1f / 16f).Within(0.000001f));
            Assert.That(root.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            Assert.That(assets.OfType<Mesh>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Texture2D>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<Material>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimationClip>().Count(), Is.EqualTo(1));
            Assert.That(assets.OfType<AnimatorController>().Count(), Is.EqualTo(1));
            Assert.That(root.GetComponent<Animator>(), Is.Not.Null);
            Assert.That(assets.OfType<Mesh>().Single().bounds.size.x,
                Is.EqualTo(2f / 16f).Within(0.000001f));
            BoxCollider collider = root.transform.Find("root/hitbox").GetComponent<BoxCollider>();
            Assert.That(collider.center, Is.EqualTo(Vector3.zero));
            Assert.That(collider.size, Is.EqualTo(Vector3.one * (4f / 16f)));
            Color pixel = assets.OfType<Texture2D>().Single().GetPixel(8, 8);
            Assert.That(pixel.r, Is.EqualTo(1f).Within(1f / 255f));
            Assert.That(pixel.g, Is.EqualTo(0.4f).Within(1f / 255f));
            Assert.That(pixel.b, Is.EqualTo(0f).Within(1f / 255f));
            Assert.That(pixel.a, Is.EqualTo(1f).Within(1f / 255f));
            Assert.That(AnimationUtility.GetAnimationClipSettings(assets.OfType<AnimationClip>().Single()).loopTime,
                Is.True);
            AnimationClip clip = assets.OfType<AnimationClip>().Single();
            var rotation = new Quaternion(
                EvaluateCurve(clip, "m_LocalRotation.x", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.y", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.z", 0.5f),
                EvaluateCurve(clip, "m_LocalRotation.w", 0.5f));
            Quaternion expected = CoordinateConverter.ConvertRotation(0f, 45f, 0f);
            Assert.That(Quaternion.Angle(rotation.normalized, expected), Is.LessThan(0.1f));
            Assert.That(EvaluateCurve(clip, "m_LocalPosition.y", 1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CustomImportScale_IsBakedWithoutScalingPrefabRoot()
        {
            var importer = AssetImporter.GetAtPath(AssetPath) as BlockbenchImporter;
            Assert.That(importer, Is.Not.Null);
            importer.ImportScale = 0.5f;
            importer.SaveAndReimport();

            var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.transform.Find("root/body").localPosition.y,
                Is.EqualTo(0.5f).Within(0.000001f));
            Assert.That(assets.OfType<Mesh>().Single().bounds.size.x,
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(EvaluateCurve(assets.OfType<AnimationClip>().Single(), "m_LocalPosition.y", 1f),
                Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void Reimport_PreservesSubAssetLocalIdentifiers()
        {
            string before = LocalIdSnapshot();
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(LocalIdSnapshot(), Is.EqualTo(before));
        }

        [Test]
        public void PostImportHook_CanCustomizeRootAndAddSubAsset()
        {
            var callbackCount = 0;
            var callbackSawGeneratedAssets = false;
            BlockbenchImportHookTestPostprocessor.PathToHandle = AssetPath;
            BlockbenchImportHookTestPostprocessor.Callback = args =>
            {
                callbackCount++;
                var generatedAssets = new List<Object>();
                args.context.GetObjects(generatedAssets);
                callbackSawGeneratedAssets = generatedAssets.OfType<Mesh>().Any() &&
                                             generatedAssets.OfType<Material>().Any() &&
                                             generatedAssets.OfType<AnimationClip>().Any();
                args.rootObject.AddComponent<BoxCollider>();
                var customData = new TextAsset("created by post-import hook") { name = "Custom Import Data" };
                args.context.AddObjectToAsset("custom_import_data", customData);
            };

            try
            {
                AssetDatabase.ImportAsset(AssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var root = AssetDatabase.LoadMainAssetAtPath(AssetPath) as GameObject;
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
                Assert.That(callbackCount, Is.EqualTo(1));
                Assert.That(callbackSawGeneratedAssets, Is.True);
                Assert.That(root, Is.Not.Null);
                Assert.That(root.GetComponent<BoxCollider>(), Is.Not.Null);
                Assert.That(assets.OfType<TextAsset>().Single().text, Is.EqualTo("created by post-import hook"));
            }
            finally
            {
                BlockbenchImportHookTestPostprocessor.Reset();
            }
        }

        [Test]
        public void Import_ArmatureCreatesWeightedSkinnedMesh()
        {
            File.WriteAllText(ArmaturePath, ArmatureModel);
            AssetDatabase.ImportAsset(ArmaturePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var root = AssetDatabase.LoadMainAssetAtPath(ArmaturePath) as GameObject;
            var renderer = root.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.bones, Has.Length.EqualTo(2));
            Assert.That(renderer.sharedMesh.boneWeights, Has.Length.EqualTo(3));
            Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(renderer.sharedMesh.bounds.size.x, Is.EqualTo(1f / 16f).Within(0.000001f));
            Assert.That(renderer.sharedMesh.boneWeights.All(weight =>
                weight.boneIndex0 == 0 && Mathf.Approximately(weight.weight0, 1f)), Is.True);
        }

        private static string LocalIdSnapshot()
        {
            return string.Join("|", AssetDatabase.LoadAllAssetsAtPath(AssetPath)
                .Where(asset => asset is GameObject || asset is Mesh || asset is Material ||
                                asset is Texture2D || asset is AnimationClip || asset is AnimatorController)
                .OrderBy(asset => asset.GetType().Name).ThenBy(asset => asset.name)
                .Select(asset =>
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long id);
                    return asset.GetType().Name + ":" + asset.name + ":" + id;
                }));
        }

        private static float EvaluateCurve(AnimationClip clip, string property, float time)
        {
            EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip).Single(candidate =>
                candidate.path == "root" && candidate.propertyName == property);
            return AnimationUtility.GetEditorCurve(clip, binding).Evaluate(time);
        }
    }

    internal sealed class BlockbenchImportHookTestPostprocessor : AssetPostprocessor
    {
        internal static string PathToHandle;
        internal static System.Action<BlockbenchImporter.ImportEventArgs> Callback;

        private void OnPreprocessAsset()
        {
            if (assetPath != PathToHandle || !(assetImporter is BlockbenchImporter importer))
                return;

            importer.OnPostBlockbenchImport -= HandlePostImport;
            importer.OnPostBlockbenchImport += HandlePostImport;
        }

        private static void HandlePostImport(BlockbenchImporter.ImportEventArgs args)
        {
            Callback?.Invoke(args);
        }

        internal static void Reset()
        {
            PathToHandle = null;
            Callback = null;
        }
    }
}
